using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Formatting.Compact;
using Wros.Api.Controllers;
using Wros.Api.Modules.Identity;
using Wros.Api.Shared.Configuration;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Health;
using Wros.Api.Shared.Observability;
using Wros.Api.Shared.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);
builder.Services.AddSerilog((services, logger) => logger
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(new RenderedCompactJsonFormatter()));

builder.Services.AddOptions<DatabaseOptions>().BindConfiguration("ConnectionStrings")
    .Validate(DatabaseOptions.IsValid, "Set ConnectionStrings:DefaultConnection to a valid SQL Server connection with a database name and Persist Security Info disabled.")
    .Validate(DatabaseOptions.IsSupportedOnCurrentPlatform,
        "SQL Server LocalDB is Windows-only. On macOS/Linux, set ConnectionStrings:DefaultConnection through User Secrets or environment variables to a SQL Server TCP endpoint (for example localhost,1433).")
    .Validate(options => builder.Environment.IsDevelopment() ||
        DatabaseOptions.IsSecure(options),
        "Outside Development, SQL encryption and a trusted server certificate are required.")
    .ValidateOnStart();
builder.Services.AddOptions<JwtOptions>().BindConfiguration("Jwt").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<CorsOptions>().BindConfiguration("Cors")
    .Validate(CorsOptions.IsValid, "Cors:AllowedOrigins must contain explicit HTTP(S) origins without paths or wildcards.")
    .Validate(options => builder.Environment.IsDevelopment() || options.AllowedOrigins.All(x => x.StartsWith("https://", StringComparison.Ordinal)),
        "Outside Development, CORS origins must use HTTPS.").ValidateOnStart();
builder.Services.AddOptions<InventoryOptions>().BindConfiguration("Inventory").ValidateDataAnnotations().ValidateOnStart();
builder.Services.AddOptions<IntegrationOptions>().BindConfiguration("Integration")
    .Validate(options => options.AllowedClients.Length == 0,
        "Integration clients are not supported until Phase 7. Keep Integration:AllowedClients empty.").ValidateOnStart();

builder.Services.AddDbContext<WrosDbContext>((services, options) =>
{
    options.UseSqlServer(services.GetRequiredService<IOptions<DatabaseOptions>>().Value.DefaultConnection,
        sql => sql.CommandTimeout(10));
    if (builder.Environment.IsDevelopment())
    {
        options.UseSeeding((context, _) => DevelopmentUserSeed.RunAsync((WrosDbContext)context,
            services.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<AppUser>>(), CancellationToken.None).GetAwaiter().GetResult());
        options.UseAsyncSeeding((context, _, cancellationToken) => DevelopmentUserSeed.RunAsync((WrosDbContext)context,
            services.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<AppUser>>(), cancellationToken));
    }
});
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .WithMethods("GET").WithHeaders("Content-Type", CorrelationMiddleware.HeaderName)
    .WithExposedHeaders(CorrelationMiddleware.HeaderName)));
builder.Services.AddWrosIdentity();
builder.Services.AddControllers(options => options.Conventions.Add(
    new DevelopmentOnlyConvention(builder.Environment.IsDevelopment())));
builder.Services.AddOpenApi();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
{
    context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
    context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
});
builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

await using var app = builder.Build();
app.UseMiddleware<CorrelationMiddleware>();
app.UseSerilogRequestLogging();
app.UseExceptionHandler(new ExceptionHandlerOptions
{
    // Handled exceptions are logged safely by ApiExceptionHandler, without raw messages.
    SuppressDiagnosticsCallback = _ => true
});
app.UseStatusCodePages();
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.Use(IdentityRegistration.UseSessionGuards);
if (app.Environment.IsDevelopment()) app.MapOpenApi().AllowAnonymous();
app.MapControllers();
app.MapHealthChecks("/api/v1/health/live", HealthOptions(false)).AllowAnonymous();
app.MapHealthChecks("/api/v1/health/ready", HealthOptions(true)).AllowAnonymous();
app.Run();

static HealthCheckOptions HealthOptions(bool readiness) => new()
{
    Predicate = registration => readiness && registration.Tags.Contains("ready"),
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await JsonSerializer.SerializeAsync(context.Response.Body, new
        {
            status = report.Status.ToString(),
            correlationId = context.TraceIdentifier,
            checks = report.Entries.Select(entry => new { name = entry.Key, status = entry.Value.Status.ToString() })
        }, cancellationToken: context.RequestAborted);
    }
};
