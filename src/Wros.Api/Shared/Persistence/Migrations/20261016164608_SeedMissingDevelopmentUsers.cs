using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Migrations;
using Wros.Api.Modules.Identity;

#nullable disable

namespace Wros.Api.Shared.Persistence.Migrations;

public partial class SeedMissingDevelopmentUsers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var setting = Environment.GetEnvironmentVariable("WROS_SEED_DEVELOPMENT_USERS");
        if (setting is not null && !bool.TryParse(setting, out _))
            throw new InvalidOperationException("WROS_SEED_DEVELOPMENT_USERS must be true or false.");
        var enabled = bool.TryParse(setting, out var requested) && requested;
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        if (!enabled)
        {
            // Explicit non-Development environments never require demo accounts.
            // Without an explicit environment, fail safely rather than silently skip a missing admin.
            if (!string.IsNullOrEmpty(environment)
                && !string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase)) return;
            migrationBuilder.Sql("""
                IF NOT EXISTS (SELECT 1 FROM [AspNetUsers] WHERE [NormalizedUserName] = N'WROS.ADMIN')
                    OR NOT EXISTS (SELECT 1 FROM [AspNetUsers] WHERE [NormalizedUserName] = N'WROS.STAFF')
                    THROW 50001, 'Development accounts are missing. Set DOTNET_ENVIRONMENT=Development, WROS_SEED_DEVELOPMENT_USERS=true, WROS_SEED_SUPERADMIN_PASSWORD and WROS_SEED_WAREHOUSESTAFF_PASSWORD before rerunning database update. For non-Development explicitly set the deployment environment.', 1;
                """);
            return;
        }

        if (!string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Demo users are forbidden outside Development. Set DOTNET_ENVIRONMENT=Development only for a development database.");
        var adminPassword = ReadPassword("WROS_SEED_SUPERADMIN_PASSWORD");
        var staffPassword = ReadPassword("WROS_SEED_WAREHOUSESTAFF_PASSWORD");
        migrationBuilder.Sql("""
            IF NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'SUPERADMIN')
                OR NOT EXISTS (SELECT 1 FROM [AspNetRoles] WHERE [NormalizedName] = N'WAREHOUSESTAFF')
                THROW 50002, 'Required role definitions are missing. Apply or repair the Identity definition migration first.', 1;
            """);
        SeedUser(migrationBuilder, new Guid("c4e86a8d-ea49-42c0-a974-15450d094e11"), "wros.admin", "SuperAdmin", adminPassword);
        SeedUser(migrationBuilder, new Guid("c4e86a8d-ea49-42c0-a974-15450d094e12"), "wros.staff", "WarehouseStaff", staffPassword);
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
        // Preserve accounts, passwords, memberships and audit history on rollback.
    }
}
