using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Suppliers;
using Wros.Api.Modules.Catalog;
using Wros.Api.Modules.Warehouses;
namespace Wros.Api.Shared.Operations;
public static class MasterDataConfiguration
{
    public static void Apply(ModelBuilder model)
    {
        var supplier = model.Entity<Supplier>(); OperationRules.Configure(supplier); supplier.HasIndex(x => x.Code).IsUnique();
        var product = model.Entity<Product>(); OperationRules.Configure(product); product.HasIndex(x => x.Code).IsUnique(); product.HasIndex(x => x.Barcode);
        product.HasIndex(x => x.ExternalProductId).IsUnique().HasFilter("[ExternalProductId] IS NOT NULL");
        var warehouse = model.Entity<Warehouse>(); OperationRules.Configure(warehouse); warehouse.HasIndex(x => x.Code).IsUnique();
        var area = model.Entity<WarehouseArea>(); OperationRules.Configure(area); area.HasIndex(x => new { x.WarehouseId, x.Code }).IsUnique();
        area.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        var rack = model.Entity<StorageRack>(); OperationRules.Configure(rack); rack.HasIndex(x => new { x.AreaId, x.Code }).IsUnique();
        rack.HasOne(x => x.Area).WithMany().HasForeignKey(x => x.AreaId).OnDelete(DeleteBehavior.Restrict);
        var location = model.Entity<StorageLocation>(); OperationRules.Configure(location); location.HasIndex(x => new { x.RackId, x.Code }).IsUnique();
        location.HasOne(x => x.Rack).WithMany().HasForeignKey(x => x.RackId).OnDelete(DeleteBehavior.Restrict);
    }
}
