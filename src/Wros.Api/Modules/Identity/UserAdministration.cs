using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Audit;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Persistence;

namespace Wros.Api.Modules.Identity;

public sealed class UserAdministration(UserManager<AppUser> users, WrosDbContext db, AuditService audit)
{
    public async Task<PageDto<UserDto>> List(int page, int pageSize, string? search)
    {
        var query = db.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            if (search.Length > 64) throw new ApiProblemException(400, "Search must be at most 64 characters.");
            var normalized = users.NormalizeName(search.Trim());
            query = query.Where(x => x.NormalizedUserName!.Contains(normalized!));
        }
        var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.NormalizedUserName).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var ids = items.Select(x => x.Id).ToArray();
        var assignments = await (from ur in db.UserRoles join r in db.Roles on ur.RoleId equals r.Id
            where ids.Contains(ur.UserId) select new { ur.UserId, r.Name }).ToListAsync();
        return new(items.Select(x => ToDto(x, assignments.Where(a => a.UserId == x.Id).Select(a => a.Name!).ToArray())).ToArray(), page, pageSize, total);
    }

    public async Task<UserDto> Create(CreateUserRequest request)
    {
        var roles = ValidateRoles(request.Roles);
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = new AppUser { Id = Guid.NewGuid(), UserName = request.UserName, MustChangePassword = true,
            CreatedBy = audit.ActorId, UpdatedBy = audit.ActorId };
        Ensure(await users.CreateAsync(user, request.Password));
        Ensure(await users.AddToRolesAsync(user, roles));
        audit.Record("UserCreated", "User", user.Id.ToString(), newValues: new { user.IsActive, Roles = roles, user.MustChangePassword });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ToDto(user, roles);
    }

    public async Task<UserDto> Update(Guid id, UpdateUserRequest request)
    {
        var roles = ValidateRoles(request.Roles);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var user = await Get(id);
        CheckStamp(user.ConcurrencyStamp, request.ConcurrencyStamp);
        var oldRoles = (await users.GetRolesAsync(user)).ToArray();
        if (id == audit.ActorId && (!request.IsActive || !roles.Contains(PermissionCatalog.SuperAdmin)))
            throw new ApiProblemException(400, "You cannot deactivate yourself or remove your own SuperAdmin access.");
        var old = new { user.IsActive, Roles = oldRoles };
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = audit.ActorId;
        Ensure(await users.UpdateAsync(user));
        Ensure(await users.RemoveFromRolesAsync(user, oldRoles.Except(roles)));
        Ensure(await users.AddToRolesAsync(user, roles.Except(oldRoles)));
        Ensure(await users.UpdateSecurityStampAsync(user));
        audit.Record("UserUpdated", "User", id.ToString(), old, new { user.IsActive, Roles = roles });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ToDto(user, roles);
    }

    public async Task ResetPassword(Guid id, ResetPasswordRequest request)
    {
        if (id == audit.ActorId) throw new ApiProblemException(400, "Use Change password for your own account.");
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await Get(id);
        CheckStamp(user.ConcurrencyStamp, request.ConcurrencyStamp);
        // Reset tokens remain server-side; never expose or log them.
        var token = await users.GeneratePasswordResetTokenAsync(user);
        Ensure(await users.ResetPasswordAsync(user, token, request.NewPassword));
        user.MustChangePassword = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = audit.ActorId;
        Ensure(await users.UpdateAsync(user));
        audit.Record("PasswordResetByAdmin", "User", id.ToString(), newValues: new { MustChangePassword = true });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task Unlock(Guid id, string concurrencyStamp)
    {
        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await Get(id);
        CheckStamp(user.ConcurrencyStamp, concurrencyStamp);
        Ensure(await users.SetLockoutEndDateAsync(user, null));
        Ensure(await users.ResetAccessFailedCountAsync(user));
        user.UpdatedAt = DateTimeOffset.UtcNow;
        user.UpdatedBy = audit.ActorId;
        Ensure(await users.UpdateSecurityStampAsync(user));
        audit.Record("UserUnlocked", "User", id.ToString());
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    private async Task<AppUser> Get(Guid id) => await users.FindByIdAsync(id.ToString())
        ?? throw new ApiProblemException(404, "User not found.");
    private static string[] ValidateRoles(string[] roles)
    {
        if (roles.Length == 0 || roles.Distinct().Count() != roles.Length || roles.Except(PermissionCatalog.Roles).Any())
            throw new ApiProblemException(400, "Choose one or more distinct defined roles.");
        return roles;
    }
    private static UserDto ToDto(AppUser user, string[] roles) => new(user.Id, user.UserName!, user.IsActive,
        user.MustChangePassword, user.LockoutEnd > DateTimeOffset.UtcNow, user.LockoutEnd, roles,
        user.ConcurrencyStamp!, user.CreatedAt);
    public static void CheckStamp(string? actual, string supplied)
    {
        if (actual != supplied) throw new ApiProblemException(409, "This record changed. Refresh before saving again.");
    }
    public static void Ensure(IdentityResult result)
    {
        if (result.Succeeded) return;
        if (result.Errors.Any(x => x.Code == "ConcurrencyFailure"))
            throw new ApiProblemException(409, "This record changed. Refresh before saving again.");
        if (result.Errors.Any(x => x.Code == "DuplicateUserName"))
            throw new ApiProblemException(409, "That username is already in use.");
        // Only fixed Identity policy descriptions, never attempted password or username values.
        var safe = result.Errors.Where(x => x.Code.StartsWith("Password", StringComparison.Ordinal)).Select(x => x.Description).ToArray();
        throw new ApiProblemException(400, safe.Length > 0 ? string.Join(" ", safe) : "The account change could not be completed. Check the supplied values.");
    }
}
