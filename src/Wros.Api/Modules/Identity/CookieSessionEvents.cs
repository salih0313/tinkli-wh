using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Wros.Api.Modules.Audit;
using Wros.Api.Shared.Persistence;

namespace Wros.Api.Modules.Identity;

public sealed class CookieSessionEvents(SignInManager<AppUser> signIn, UserManager<AppUser> users,
    AuditService audit, WrosDbContext db) : CookieAuthenticationEvents
{
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        // Public health probes must not acquire a database dependency through a browser's cookie.
        var path = context.Request.Path;
        if (path.Equals("/api/v1/health/live", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/v1/health/ready", StringComparison.OrdinalIgnoreCase))
        {
            context.RejectPrincipal();
            return;
        }
        var user = await signIn.ValidateSecurityStampAsync(context.Principal);
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user))
        {
            audit.Record("SessionRejected", "Session");
            await db.SaveChangesAsync();
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return;
        }
        context.HttpContext.Items["MustChangePassword"] = user.MustChangePassword;
    }
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    }
    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }
}
