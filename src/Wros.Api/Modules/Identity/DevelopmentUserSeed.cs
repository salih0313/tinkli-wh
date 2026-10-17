using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Audit;
using Wros.Api.Shared.Persistence;

namespace Wros.Api.Modules.Identity;

// EF calls this file's seeding logic on database update, even with no pending migrations.
internal static class DevelopmentUserSeed
{
    public static async Task RunAsync(WrosDbContext db, IPasswordHasher<AppUser> hasher, CancellationToken cancellationToken)
    {
        await using var transaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        await AddIfMissing(db, hasher, new Guid("c4e86a8d-ea49-42c0-a974-15450d094e11"),
            "wros.admin", "SuperAdmin", DevelopmentSeedCredentials.SuperAdminPassword, cancellationToken);
        await AddIfMissing(db, hasher, new Guid("c4e86a8d-ea49-42c0-a974-15450d094e12"),
            "wros.staff", "WarehouseStaff", DevelopmentSeedCredentials.WarehouseStaffPassword, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }

    private static async Task AddIfMissing(WrosDbContext db, IPasswordHasher<AppUser> hasher, Guid id,
        string userName, string roleName, string password, CancellationToken cancellationToken)
    {
        var normalized = userName.ToUpperInvariant();
        if (await db.Users.AnyAsync(x => x.NormalizedUserName == normalized, cancellationToken)) return;
        if (password.Length < 8)
            throw new InvalidOperationException($"Set the {roleName} password in Modules/Identity/DevelopmentSeedCredentials.local.cs. Use at least 8 characters. No seed environment variables are required.");
        var role = await db.Roles.SingleOrDefaultAsync(x => x.NormalizedName == roleName.ToUpperInvariant(), cancellationToken)
            ?? throw new InvalidOperationException("Apply Identity role migrations before creating Development users.");
        var user = new AppUser
        {
            Id = id, UserName = userName, NormalizedUserName = normalized, IsActive = true,
            LockoutEnabled = true, SecurityStamp = Guid.NewGuid().ToString("N"), ConcurrencyStamp = Guid.NewGuid().ToString("N")
        };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = user.Id, RoleId = role.Id });
        db.AuditLogs.Add(new AuditLog
        {
            Action = "DevelopmentUserSeeded", Entity = "User", EntityId = user.Id.ToString(),
            NewValues = JsonSerializer.Serialize(new { Role = roleName, IsActive = true }), CorrelationId = "migration-seed"
        });
    }
}
