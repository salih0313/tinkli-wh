using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;
using Wros.Api.Modules.Identity;

#nullable disable

namespace Wros.Api.Shared.Persistence.Migrations;

public partial class SeedIdentityData : Migration
{
    // Historical migration constants: do not depend on a catalog that future phases may change.
    private static readonly string[] Permissions = ["users.manage", "roles.manage", "suppliers.manage", "products.manage",
        "purchaseorders.manage", "receiving.manage", "inventory.view", "inventory.adjust", "inventory.transfer",
        "outbound.manage", "integration.manage", "audit.view"];
    private static readonly string[] Roles = ["SuperAdmin", "WarehouseManager", "WarehouseStaff", "PurchasingStaff", "InventoryController", "Viewer"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var seedUsers = Environment.GetEnvironmentVariable("WROS_SEED_DEVELOPMENT_USERS");
        if (seedUsers is not null && !bool.TryParse(seedUsers, out _))
            throw new InvalidOperationException("WROS_SEED_DEVELOPMENT_USERS must be true or false.");
        var enabled = string.Equals(seedUsers, "true", StringComparison.OrdinalIgnoreCase);
        string adminPassword = null;
        string staffPassword = null;
        if (enabled)
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Development user migration seeding requires DOTNET_ENVIRONMENT=Development (or ASPNETCORE_ENVIRONMENT=Development). Demo users are forbidden outside Development.");
            adminPassword = ReadPassword("WROS_SEED_SUPERADMIN_PASSWORD");
            staffPassword = ReadPassword("WROS_SEED_WAREHOUSESTAFF_PASSWORD");
        }

        foreach (var permission in Permissions)
            migrationBuilder.Sql($"IF NOT EXISTS (SELECT 1 FROM [Permissions] WHERE [Id] = {Sql(permission)}) INSERT INTO [Permissions] ([Id]) VALUES ({Sql(permission)});");

        for (var index = 0; index < Roles.Length; index++)
        {
            var role = Roles[index];
            var roleId = $"83fb5340-0241-4800-a218-{index + 1:000000000000}";
            var grants = role == "SuperAdmin" ? Permissions : ["inventory.view"];
            var inserts = string.Join("\n", grants.Select(permission =>
                $"INSERT INTO [RolePermissions] ([RoleId], [PermissionId]) VALUES ('{roleId}', {Sql(permission)});"));
            migrationBuilder.Sql($"""
                IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = {Sql(role.ToUpperInvariant())})
                BEGIN
                    INSERT INTO [AspNetRoles] ([Id], [Name], [NormalizedName], [ConcurrencyStamp])
                    VALUES ('{roleId}', {Sql(role)}, {Sql(role.ToUpperInvariant())}, CONVERT(nvarchar(36), NEWID()));
                    {inserts}
                    INSERT INTO [AuditLogs] ([Id], [ActorId], [Timestamp], [Action], [Entity], [EntityId], [OldValues], [NewValues], [CorrelationId])
                    VALUES (NEWID(), NULL, SYSUTCDATETIME(), N'RoleSeeded', N'Role', N'{roleId}', NULL,
                        {Sql(System.Text.Json.JsonSerializer.Serialize(new { Name = role, Permissions = grants }))}, N'migration-seed');
                END;
                """);
        }

        if (enabled)
        {
            SeedUser(migrationBuilder, new Guid("c4e86a8d-ea49-42c0-a974-15450d094e11"), "wros.admin", "SuperAdmin", adminPassword);
            SeedUser(migrationBuilder, new Guid("c4e86a8d-ea49-42c0-a974-15450d094e12"), "wros.staff", "WarehouseStaff", staffPassword);
        }
    }

    private static void SeedUser(MigrationBuilder migrationBuilder, Guid id, string userName, string role, string password)
    {
        var user = new AppUser { Id = id, UserName = userName };
        var hash = new PasswordHasher<AppUser>().HashPassword(user, password);
        // Only the Identity hash is placed in the transient SQL command. Never the plaintext password.
        // Existing users are skipped entirely: never reset passwords, activation or memberships.
        migrationBuilder.Sql($"""
            IF NOT EXISTS (SELECT 1 FROM [AspNetUsers] WHERE [NormalizedUserName] = {Sql(userName.ToUpperInvariant())})
            BEGIN
                INSERT INTO [AspNetUsers] ([Id], [IsActive], [MustChangePassword], [CreatedAt], [UpdatedAt], [CreatedBy], [UpdatedBy],
                    [UserName], [NormalizedUserName], [Email], [NormalizedEmail], [EmailConfirmed], [PasswordHash],
                    [SecurityStamp], [ConcurrencyStamp], [PhoneNumber], [PhoneNumberConfirmed], [TwoFactorEnabled],
                    [LockoutEnd], [LockoutEnabled], [AccessFailedCount])
                VALUES ('{id:D}', 1, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, NULL,
                    {Sql(userName)}, {Sql(userName.ToUpperInvariant())}, NULL, NULL, 0, {Sql(hash)},
                    CONVERT(nvarchar(36), NEWID()), CONVERT(nvarchar(36), NEWID()), NULL, 0, 0, NULL, 1, 0);
                INSERT INTO [AspNetUserRoles] ([UserId], [RoleId])
                SELECT '{id:D}', [Id] FROM [AspNetRoles] WHERE [NormalizedName] = {Sql(role.ToUpperInvariant())};
                INSERT INTO [AuditLogs] ([Id], [ActorId], [Timestamp], [Action], [Entity], [EntityId], [OldValues], [NewValues], [CorrelationId])
                VALUES (NEWID(), NULL, SYSUTCDATETIME(), N'DevelopmentUserSeeded', N'User', N'{id:D}', NULL,
                    {Sql(System.Text.Json.JsonSerializer.Serialize(new { Role = role, IsActive = true }))}, N'migration-seed');
            END;
            """);
    }

    private static string ReadPassword(string name)
    {
        var password = Environment.GetEnvironmentVariable(name);
        if (password is null || password.Length is < 8 or > 128 || password.Distinct().Count() < 4
            || !password.Any(c => c is >= 'A' and <= 'Z') || !password.Any(c => c is >= 'a' and <= 'z')
            || !password.Any(c => c is >= '0' and <= '9') || !password.Any(c => !((c is >= 'A' and <= 'Z') || (c is >= 'a' and <= 'z') || (c is >= '0' and <= '9'))))
            throw new InvalidOperationException($"Set {name} securely in the migration process environment. Password policy: 8–128 characters with uppercase, lowercase, a number, a symbol and at least four distinct characters.");
        return password;
    }

    private static string Sql(string value) => "N'" + value.Replace("'", "''") + "'";

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately preserve users, grants and audit history on schema rollback.
        // A data seed rollback must never delete real accounts or their later activity.
    }
}
