using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Audit;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Persistence;

namespace Wros.Api.Modules.Identity;

public sealed class RoleAdministration(RoleManager<AppRole> roles, WrosDbContext db, AuditService audit)
{
    public async Task<RoleDto[]> List()
    {
        var all = await db.Roles.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        var grants = await db.RolePermissions.AsNoTracking().ToListAsync();
        return all.Select(r => new RoleDto(r.Id, r.Name!, grants.Where(x => x.RoleId == r.Id)
            .Select(x => x.PermissionId).OrderBy(x => x).ToArray(), r.ConcurrencyStamp!, r.Name == PermissionCatalog.SuperAdmin)).ToArray();
    }

    public async Task SetPermissions(Guid id, RolePermissionsRequest request)
    {
        if (request.Permissions.Distinct().Count() != request.Permissions.Length || request.Permissions.Except(PermissionCatalog.All).Any())
            throw new ApiProblemException(400, "Choose distinct defined permissions.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var role = await roles.FindByIdAsync(id.ToString()) ?? throw new ApiProblemException(404, "Role not found.");
        if (role.Name == PermissionCatalog.SuperAdmin) throw new ApiProblemException(400, "SuperAdmin always has all permissions.");
        UserAdministration.CheckStamp(role.ConcurrencyStamp, request.ConcurrencyStamp);
        // Identity's concurrency stamp serializes competing replacements of this role's grants.
        UserAdministration.Ensure(await roles.UpdateAsync(role));
        var old = await db.RolePermissions.Where(x => x.RoleId == id).ToListAsync();
        db.RolePermissions.RemoveRange(old);
        // Reuse unchanged tracked rows to avoid duplicate-key tracking conflicts.
        foreach (var grant in old.Where(x => request.Permissions.Contains(x.PermissionId))) db.Entry(grant).State = EntityState.Unchanged;
        foreach (var permission in request.Permissions.Except(old.Select(x => x.PermissionId)))
            db.RolePermissions.Add(new RolePermission { RoleId = id, PermissionId = permission });
        audit.Record("RolePermissionsChanged", "Role", id.ToString(),
            new { Permissions = old.Select(x => x.PermissionId).OrderBy(x => x).ToArray() },
            new { Permissions = request.Permissions.OrderBy(x => x).ToArray() });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }
}
