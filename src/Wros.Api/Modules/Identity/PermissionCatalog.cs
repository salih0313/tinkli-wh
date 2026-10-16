namespace Wros.Api.Modules.Identity;

public static class PermissionCatalog
{
    public const string SuperAdmin = "SuperAdmin";
    public const string ManageUsersPolicy = "ManageUsers";
    public const string ManageRolesPolicy = "ManageRoles";
    public const string AuditPolicy = "ViewAudit";
    public static readonly string[] All = ["users.manage", "roles.manage", "suppliers.manage", "products.manage",
        "purchaseorders.manage", "receiving.manage", "inventory.view", "inventory.adjust", "inventory.transfer",
        "outbound.manage", "integration.manage", "audit.view"];
    public static readonly string[] Roles = [SuperAdmin, "WarehouseManager", "WarehouseStaff", "PurchasingStaff", "InventoryController", "Viewer"];

    // Initial seed grants are frozen in the SeedIdentityData migration.
}
