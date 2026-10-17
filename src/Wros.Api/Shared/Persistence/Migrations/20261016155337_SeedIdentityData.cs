using Microsoft.EntityFrameworkCore.Migrations;

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

        // Development account creation is handled by EF UseSeeding after migrations.
    }

    private static string Sql(string value) => "N'" + value.Replace("'", "''") + "'";

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Deliberately preserve users, grants and audit history on schema rollback.
        // A data seed rollback must never delete real accounts or their later activity.
    }
}
