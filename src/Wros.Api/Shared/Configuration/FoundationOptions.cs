using System.ComponentModel.DataAnnotations;
using Microsoft.Data.SqlClient;

namespace Wros.Api.Shared.Configuration;

public sealed class DatabaseOptions
{
    public string DefaultConnection { get; set; } = "";

    public static bool IsValid(DatabaseOptions options)
    {
        try
        {
            var connection = new SqlConnectionStringBuilder(options.DefaultConnection);
            return !string.IsNullOrWhiteSpace(connection.DataSource)
                && !string.IsNullOrWhiteSpace(connection.InitialCatalog)
                && !connection.PersistSecurityInfo;
        }
        catch (ArgumentException) { return false; }
    }
    public static bool IsSupportedOnCurrentPlatform(DatabaseOptions options)
    {
        if (OperatingSystem.IsWindows() || !IsValid(options)) return true;
        var sql = new SqlConnectionStringBuilder(options.DefaultConnection);
        return !sql.DataSource.TrimStart().StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsSecure(DatabaseOptions options)
    {
        if (!IsValid(options)) return false;
        var sql = new SqlConnectionStringBuilder(options.DefaultConnection);
        return sql.Encrypt.ToString() != "False" && !sql.TrustServerCertificate;
    }
}

public sealed class JwtOptions
{
    [Required] public string Issuer { get; set; } = "";
    [Required] public string Audience { get; set; } = "";
    [Range(1, 60)] public int AccessTokenMinutes { get; set; }
}

public sealed class CorsOptions
{
    public string[] AllowedOrigins { get; set; } = [];
    public static bool IsValid(CorsOptions options) => options.AllowedOrigins.Length > 0
        && options.AllowedOrigins.All(origin => !origin.Contains('*')
            && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
            && (uri.Scheme == "https" || uri.Scheme == "http")
            && uri.GetLeftPart(UriPartial.Authority) == origin
            && string.IsNullOrEmpty(uri.UserInfo));
}

public sealed class InventoryOptions
{
    [Range(0, 3650)] public int ExpiryAlertDays { get; set; } = 30;
}

public sealed class WrosSessionOptions
{
    public bool CrossSiteCookies { get; set; }
    [Range(1, 60)] public int SessionMinutes { get; set; } = 15;
}

// Retained configuration flags for compatibility; account seeding is owned by an EF migration.
public sealed class SeedOptions
{
    public bool Enabled { get; set; }
    public bool SampleDataEnabled { get; set; }
}

public sealed class IntegrationOptions
{
    public string[] AllowedClients { get; set; } = [];
    public string Audience { get; set; } = "https://localhost:7243/api/v1/integration";
    public int SnapshotMinutes { get; set; } = 30;
    public int MaxSnapshotRows { get; set; } = 100000;
    public IntegrationClientOptions[] Clients { get; set; } = [];

    public static bool IsValid(IntegrationOptions options)
    {
        if (!Uri.TryCreate(options.Audience, UriKind.Absolute, out var audience)
            || audience.Scheme != "https" || !string.IsNullOrEmpty(audience.UserInfo)
            || !string.IsNullOrEmpty(audience.Fragment) || !string.IsNullOrEmpty(audience.Query)
            || options.SnapshotMinutes is < 1 or > 1440 || options.MaxSnapshotRows is < 100 or > 1000000
            || options.AllowedClients is null || options.Clients is null
            || options.AllowedClients.Length > 100 || options.Clients.Length > 100)
            return false;
        if (options.Clients.Any(client => client is null)
            || options.AllowedClients.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.AllowedClients.Length
            || options.Clients.Select(client => client.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Clients.Length)
            return false;
        foreach (var client in options.Clients)
        {
            if (string.IsNullOrEmpty(client.Id)
                || !System.Text.RegularExpressions.Regex.IsMatch(client.Id, "^[A-Za-z0-9._-]{1,80}$")
                || client.Keys is null || client.Keys.Length is < 1 or > 3
                || client.Keys.Any(key => key is null)
                || client.Keys.Select(key => key.KeyId).Distinct().Count() != client.Keys.Length
                || client.Scopes is null || client.Scopes.Length is < 1 or > 2
                || client.Scopes.Distinct().Count() != client.Scopes.Length
                || client.Scopes.Any(scope => scope is not ("inventory.snapshot" or "orders.deduct"))
                || client.Keys.Any(key => !IsValidPublicKey(key)))
                return false;
        }
        return options.AllowedClients.All(id => options.Clients.Any(client => client.Id == id));
    }

    private static bool IsValidPublicKey(IntegrationPublicKey key)
    {
        if (string.IsNullOrWhiteSpace(key.KeyId) || key.KeyId.Length > 80
            || string.IsNullOrWhiteSpace(key.PublicKeyPem)
            || key.PublicKeyPem.Contains("PRIVATE", StringComparison.Ordinal))
            return false;
        try
        {
            using var rsa = System.Security.Cryptography.RSA.Create();
            rsa.ImportFromPem(key.PublicKeyPem);
            return rsa.KeySize >= 2048;
        }
        catch (ArgumentException) { return false; }
        catch (System.Security.Cryptography.CryptographicException) { return false; }
    }
}

public sealed class IntegrationClientOptions
{
    public string Id { get; set; } = "";
    public string[] Scopes { get; set; } = [];
    public IntegrationPublicKey[] Keys { get; set; } = [];
}

public sealed class IntegrationPublicKey
{
    public string KeyId { get; set; } = "";
    public string PublicKeyPem { get; set; } = "";
}
