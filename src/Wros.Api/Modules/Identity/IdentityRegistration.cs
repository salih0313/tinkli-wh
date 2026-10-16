using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Wros.Api.Modules.Audit;
using Wros.Api.Shared.Configuration;
using Wros.Api.Shared.Persistence;

namespace Wros.Api.Modules.Identity;

public static class IdentityRegistration
{
    public static IServiceCollection AddWrosIdentity(this IServiceCollection services)
    {
        services.AddOptions<WrosSessionOptions>().BindConfiguration("Authentication").ValidateDataAnnotations().ValidateOnStart();
        services.AddOptions<SeedOptions>().BindConfiguration("Seed")
            .Validate(options => !options.Enabled, "User seeding now runs in the EF migration using WROS_SEED_DEVELOPMENT_USERS and secure environment credentials. Keep Seed:Enabled false.")
            .Validate(options => !options.SampleDataEnabled, "Sample business data is not implemented in Phase 1.")
            .ValidateOnStart();
        services.AddDataProtection().SetApplicationName("Wros");
        services.AddHttpContextAccessor();
        services.AddScoped<AuditService>();
        services.AddScoped<SessionService>();
        services.AddScoped<UserAdministration>();
        services.AddScoped<RoleAdministration>();
        services.AddScoped<CookieSessionEvents>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler, AuthorizationAuditHandler>();
        services.AddIdentity<AppUser, AppRole>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequiredUniqueChars = 4;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        }).AddEntityFrameworkStores<WrosDbContext>().AddDefaultTokenProviders();
        services.AddOptions<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<Microsoft.Extensions.Options.IOptions<WrosSessionOptions>>((cookie, session) =>
            {
                cookie.Cookie.Name = "__Host-Wros.Session";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                cookie.Cookie.SameSite = SameSiteMode.Strict;
                cookie.Cookie.Path = "/";
                cookie.ExpireTimeSpan = TimeSpan.FromMinutes(session.Value.SessionMinutes);
                cookie.SlidingExpiration = false;
                cookie.EventsType = typeof(CookieSessionEvents);
            });
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-Wros.Csrf";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
            options.Cookie.Path = "/";
        });
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
            options.AddPolicy(PermissionCatalog.ManageUsersPolicy, policy => policy.RequireAuthenticatedUser()
                .RequireRole(PermissionCatalog.SuperAdmin).AddRequirements(new PermissionRequirement("users.manage")));
            options.AddPolicy(PermissionCatalog.ManageRolesPolicy, policy => policy.RequireAuthenticatedUser()
                .RequireRole(PermissionCatalog.SuperAdmin).AddRequirements(new PermissionRequirement("roles.manage")));
            options.AddPolicy(PermissionCatalog.AuditPolicy, policy => policy.RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement("audit.view")));
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = 15, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("sensitive", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });
        return services;
    }

    public static async Task UseSessionGuards(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value;
        if (context.User.Identity?.IsAuthenticated == true && context.Items["MustChangePassword"] is true
            && path is not ("/api/v1/auth/me" or "/api/v1/auth/csrf" or "/api/v1/auth/change-password" or "/api/v1/auth/logout"))
        {
            await Results.Problem(statusCode: 403, title: "Change your temporary password before continuing.").ExecuteAsync(context);
            return;
        }
        // All state changes, including login/logout, require an antiforgery token.
        if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
        {
            try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                await Results.Problem(statusCode: 400, title: "Invalid or expired CSRF token. Refresh and retry.").ExecuteAsync(context);
                return;
            }
        }
        await next(context);
    }
}
