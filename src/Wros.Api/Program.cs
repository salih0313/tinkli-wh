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
    .Validate(IntegrationOptions.IsValid,
        "Integration configuration requires an HTTPS audience, valid limits, explicit allowed clients, minimal scopes and RSA public keys (2048+ bits).").ValidateOnStart();

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
    .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
    .WithHeaders("Content-Type", "X-CSRF-TOKEN", CorrelationMiddleware.HeaderName)
    .AllowCredentials()
    .WithExposedHeaders(CorrelationMiddleware.HeaderName)));
builder.Services.AddWrosIdentity();
builder.Services.AddOptions<ProductionAdminSeedOptions>().BindConfiguration("ProductionAdminSeed")
    .Validate(options => !options.Enabled || builder.Environment.IsProduction(),
        "ProductionAdminSeed may only be enabled in Production.").ValidateOnStart();
builder.Services.AddAuthentication().AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, Wros.Api.Modules.Integration.IntegrationAuthentication>(Wros.Api.Modules.Integration.IntegrationAuthentication.SchemeName,_=>{});
builder.Services.AddAuthorization(options => {
    foreach(var scope in new[]{"inventory.snapshot","orders.deduct"}) options.AddPolicy("Machine:"+scope,p=>p.AddAuthenticationSchemes(Wros.Api.Modules.Integration.IntegrationAuthentication.SchemeName).RequireAuthenticatedUser().RequireClaim("integration_client").RequireClaim("scope",scope));
});
builder.Services.AddRateLimiter(options=>options.AddPolicy("integration",context=>System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new System.Threading.RateLimiting.FixedWindowRateLimiterOptions{PermitLimit=120,Window=TimeSpan.FromMinutes(1),QueueLimit=0})));
builder.Services.AddScoped<Wros.Api.Modules.Integration.IntegrationService>();
builder.Services.AddScoped<Wros.Api.Shared.Operations.MasterDataService>();
builder.Services.AddScoped<Wros.Api.Shared.Operations.DocumentNumbers>();
builder.Services.AddOptions<Wros.Api.Modules.Purchasing.SupplierCatalog.SupplierImportOptions>().BindConfiguration("SupplierImport")
    .ValidateDataAnnotations().Validate(o => o.DefaultCurrency == "KWD" && o.AllowedExtensions is [".xlsx"], "Supplier imports currently support KWD and .xlsx only.").ValidateOnStart();
builder.Services.AddScoped<Wros.Api.Modules.Purchasing.SupplierCatalog.SupplierCatalogService>();
builder.Services.AddOptions<Wros.Api.Modules.Purchasing.PurchaseOrderPrintOptions>().BindConfiguration("PurchaseOrderPrint")
    .ValidateDataAnnotations().Validate(o => o.PageSize == "A4", "PO print currently supports A4 only.").ValidateOnStart();
builder.Services.AddScoped<Wros.Api.Modules.Purchasing.PurchaseOrderPrintService>();
builder.Services.AddScoped<Wros.Api.Modules.Purchasing.PurchaseOrderService>();
builder.Services.AddScoped<Wros.Api.Modules.Receiving.ReceivingService>();
builder.Services.AddScoped<Wros.Api.Modules.Receiving.ReceiptQueries>();
builder.Services.AddScoped<Wros.Api.Modules.Inventory.InventoryQueries>();
builder.Services.AddScoped<Wros.Api.Modules.Outbound.StockTransactionService>();
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
builder.Services.AddHostedService<DatabaseMigrationService>();

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
if (app.Environment.IsDevelopment())
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
            Path.Combine(AppContext.BaseDirectory, "BrandingAssets")),
        RequestPath = "/branding"
    });
    app.MapOpenApi().AllowAnonymous();
    app.MapSwaggerUI("swagger", options =>
    {
        options.DocumentTitle = "Tinkli kids planet Warehouse API";
        options.HeadContent = """
            <link rel="icon" type="image/png" href="../branding/fav/favicon-32x32.png" />
            <link rel="shortcut icon" href="../branding/fav/favicon.ico" />
            <link rel="apple-touch-icon" href="../branding/fav/apple-touch-icon.png" />
            <meta name="theme-color" content="#6147a6" />
            <link rel="stylesheet" href="../branding/swagger.css" />
            """;
        options.SwaggerEndpoint("../openapi/v1.json", "WROS API v1");
        options.EnableFilter();
        options.DisplayRequestDuration();
        // Fetch a fresh token for each write: login/logout changes the token's identity.
        // Keep cookies HttpOnly and retain the normal server-side CSRF validation.
        options.UseRequestInterceptor("""
            async (request) => {
                const url = new URL(request.url, window.location.href);
                if (url.origin === window.location.origin && url.pathname.startsWith('/api/v1/')) {
                    request.credentials = 'same-origin';
                    if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(request.method.toUpperCase())) {
                        const response = await fetch('../api/v1/auth/csrf', {
                            credentials: 'same-origin', cache: 'no-store'
                        });
                        if (!response.ok) throw new Error('Unable to obtain a CSRF token. Refresh and retry.');
                        const body = await response.json();
                        request.headers['X-CSRF-TOKEN'] = body.token;
                    }
                }
                return request;
            }
            """);
    }).AllowAnonymous();
}
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
