using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wros.Api.Modules.Audit;
using Wros.Api.Shared.Persistence;

namespace Wros.Api.Modules.Identity;

public sealed class ProductionAdminSeedOptions
{
    public bool Enabled { get; set; }
    public string UserName { get; set; } = "";
    public string Password { get; set; } = "";
}

internal static class ProductionAdminSeed
{
    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var options = services.GetRequiredService<IOptions<ProductionAdminSeedOptions>>().Value;
        if (!options.Enabled) return;
        var db = services.GetRequiredService<WrosDbContext>();
        var users = services.GetRequiredService<UserManager<AppUser>>();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        // Serialize bootstrap across concurrent API starts using a transaction-owned SQL lock.
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = 'Wros.FirstProductionAdmin',
                @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
            IF @result < 0 THROW 51000, 'Could not acquire administrator bootstrap lock.', 1;
            """, cancellationToken);
        var role = await db.Roles.SingleOrDefaultAsync(x => x.NormalizedName == "SUPERADMIN", cancellationToken)
            ?? throw new InvalidOperationException("Apply Identity role migrations before production administrator bootstrap.");
        // A historical successful bootstrap also prevents reseeding after account/role removal.
        if (await db.AuditLogs.AnyAsync(x => x.Action == "ProductionAdminSeeded", cancellationToken)
            || await db.UserRoles.AnyAsync(x => x.RoleId == role.Id, cancellationToken))
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        if (string.IsNullOrWhiteSpace(options.UserName) || options.UserName.Length > 64
            || options.UserName != options.UserName.Trim() || string.IsNullOrWhiteSpace(options.Password))
            throw new InvalidOperationException("Supply ProductionAdminSeed:UserName (1–64 characters, no surrounding whitespace) and Password through secure server configuration.");
        if (await users.FindByNameAsync(options.UserName) is not null)
            throw new InvalidOperationException("Production administrator bootstrap refuses to modify an existing username.");

        var user = new AppUser
        {
            Id = Guid.NewGuid(), UserName = options.UserName, IsActive = true,
            LockoutEnabled = true, MustChangePassword = true
        };
        Ensure(await users.CreateAsync(user, options.Password));
        Ensure(await users.AddToRoleAsync(user, PermissionCatalog.SuperAdmin));
        db.AuditLogs.Add(new AuditLog
        {
            Action = "ProductionAdminSeeded", Entity = "User", EntityId = user.Id.ToString(),
            NewValues = "{\"Role\":\"SuperAdmin\",\"MustChangePassword\":true}",
            CorrelationId = "production-admin-bootstrap"
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Production administrator bootstrap failed Identity validation. Check the configured username and password policy; no account changes were committed.");
    }
}
