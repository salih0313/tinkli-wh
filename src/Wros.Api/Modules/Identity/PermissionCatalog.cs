namespace Wros.Api.Modules.Identity;

public static class PermissionCatalog
{
    public const string SuperAdmin = "SuperAdmin";
    public const string ManageUsersPolicy = "ManageUsers";
    public const string ManageRolesPolicy = "ManageRoles";
    public const string AuditPolicy = "ViewAudit";
    public const string SuppliersPolicy = "suppliers.manage";
    public const string ProductsPolicy = "products.manage";
    public const string WarehousesPolicy = "warehouses.manage";
    public const string PurchasingPolicy = "purchaseorders.manage";
    public const string ReceivingPolicy = "receiving.manage";
    public const string InventoryPolicy = "inventory.view";
    public const string PurchasingReadPolicy = "PurchasingRead";
    public const string MasterReadPolicy = "MasterDataRead";
    public static readonly string[] MasterReaders = [SuppliersPolicy, ProductsPolicy, WarehousesPolicy, PurchasingPolicy, ReceivingPolicy, InventoryPolicy];
    public static readonly string[] All = ["users.manage", "roles.manage", "suppliers.manage", "products.manage",
        "warehouses.manage", "purchaseorders.manage", "receiving.manage", "inventory.view", "inventory.adjust", "inventory.transfer",
        "outbound.manage", "integration.manage", "audit.view"];
    public static readonly string[] Roles = [SuperAdmin, "WarehouseManager", "WarehouseStaff", "PurchasingStaff", "InventoryController", "Viewer"];

    // Initial seed grants are frozen in the SeedIdentityData migration.
}
