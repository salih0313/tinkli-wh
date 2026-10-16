using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Shared.Persistence;

namespace Wros.Api.Modules.Identity;

public sealed record PermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class PermissionHandler(WrosDbContext db) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return;
        // Live database grants: edits affect the next request, including existing sessions.
        var roleIds = db.UserRoles.Where(x => x.UserId == userId).Select(x => x.RoleId);
        var isSuperAdmin = await db.Roles.AnyAsync(x => roleIds.Contains(x.Id) && x.Name == PermissionCatalog.SuperAdmin);
        if (isSuperAdmin || await db.RolePermissions.AnyAsync(x => roleIds.Contains(x.RoleId) && x.PermissionId == requirement.Permission))
            context.Succeed(requirement);
    }
}
