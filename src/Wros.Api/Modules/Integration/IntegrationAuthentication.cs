using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Wros.Api.Shared.Configuration;
using Wros.Api.Shared.Persistence;
namespace Wros.Api.Modules.Integration;

public sealed class IntegrationAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> schemeOptions, ILoggerFactory logger, UrlEncoder encoder,
    IOptions<IntegrationOptions> configuration, WrosDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(schemeOptions, logger, encoder)
{
    public const string SchemeName = "IntegrationAssertion";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header) || !header.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();
        if (!Request.IsHttps)
            return AuthenticateResult.Fail("HTTPS is required.");
        var raw = header.ToString()[7..];
        if (raw.Length is < 20 or > 12000)
            return AuthenticateResult.Fail("Invalid integration assertion.");
        try
        {
            var handler = new JsonWebTokenHandler { MaximumTokenSizeInBytes = 12000, MapInboundClaims = false };
            var untrusted = handler.ReadJsonWebToken(raw);
            var clientId = untrusted.Issuer;
            var config = configuration.Value;
            var client = config.Clients.SingleOrDefault(x => x.Id == clientId && config.AllowedClients.Contains(x.Id));
            if (client == null || !await db.Set<IntegrationClient>().AsNoTracking().AnyAsync(x => x.Id == clientId && x.IsActive))
                return AuthenticateResult.Fail("Invalid integration assertion.");
            var keys = new List<SecurityKey>();
            var rsaKeys = new List<RSA>();
            try
            {
                foreach (var key in client.Keys)
                {
                    var rsa = RSA.Create();
                    rsaKeys.Add(rsa);
                    rsa.ImportFromPem(key.PublicKeyPem);
                    keys.Add(new RsaSecurityKey(rsa) { KeyId = key.KeyId });
                }
                var result = await handler.ValidateTokenAsync(raw, new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = client.Id, ValidateAudience = true, ValidAudience = config.Audience, IgnoreTrailingSlashWhenValidatingAudience = false, ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ValidateIssuerSigningKey = true, IssuerSigningKeys = keys, TryAllIssuerSigningKeys = false, ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.Zero });
                if (!result.IsValid || result.SecurityToken is not JsonWebToken token || token.Subject != client.Id || token.Audiences.Count() != 1 || token.Audiences.Single() != config.Audience || token.Alg != SecurityAlgorithms.RsaSha256 || token.Typ != "wros-client+jwt" || !client.Keys.Any(k => k.KeyId == token.Kid))
                    return AuthenticateResult.Fail("Invalid integration assertion.");
                var now = DateTime.UtcNow;
                if (!token.TryGetPayloadValue<long>("iat", out var iat) || !token.TryGetPayloadValue<long>("exp", out var exp) || exp <= iat || exp - iat > 120 || iat > new DateTimeOffset(now).ToUnixTimeSeconds() + 5 || iat < new DateTimeOffset(now.AddMinutes(-2)).ToUnixTimeSeconds() || !Guid.TryParse(token.Id, out _))
                    return AuthenticateResult.Fail("Invalid integration assertion.");
                var replay = new AssertionReplay { ClientId = client.Id, TokenIdHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token.Id))), ExpiresAt = new DateTimeOffset(token.ValidTo, TimeSpan.Zero) };
                db.Set<AssertionReplay>().Add(replay);
                try
                {
                    await db.SaveChangesAsync();
                }
                catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 }) { db.Entry(replay).State = EntityState.Detached; return AuthenticateResult.Fail("Assertion replay rejected."); }
                // Staff identifiers/roles are never accepted from machine assertions.
                var identity = new ClaimsIdentity(SchemeName);
                identity.AddClaim(new Claim("integration_client", client.Id));
                foreach (var scope in client.Scopes)
                    identity.AddClaim(new Claim("scope", scope));
                Context.Items["ValidatedIntegrationClient"] = client.Id;
                return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
            }
            finally { foreach (var rsa in rsaKeys) rsa.Dispose(); }
        }
        catch (Exception e) when (e is SecurityTokenException or ArgumentException or FormatException or CryptographicException or System.Text.Json.JsonException) { return AuthenticateResult.Fail("Invalid integration assertion."); }
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Problem(statusCode: 401, title: "Valid integration assertion required.", extensions: new Dictionary<string, object?> { { "correlationId", Context.TraceIdentifier } }).ExecuteAsync(Context);
    }
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties) => Results.Problem(statusCode: 403, title: "Integration scope required.").ExecuteAsync(Context);
}
