using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Wros.Api.Modules.Audit;
using Wros.Api.Shared.Persistence;

namespace Wros.Api.Modules.Identity;

public sealed class AuthorizationAuditHandler(AuditService audit, WrosDbContext db) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler fallback = new();

    public async Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult result)
    {
        if (result.Challenged || result.Forbidden)
        {
            // Route templates never contain query strings, passwords, or supplied identifiers.
            var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            audit.Record(result.Forbidden ? "AccessDenied" : "AuthenticationRequired", "Endpoint",
                newValues: new { Route = route, context.Request.Method });
            await db.SaveChangesAsync();
        }
        await fallback.HandleAsync(next, context, policy, result);
    }
}
