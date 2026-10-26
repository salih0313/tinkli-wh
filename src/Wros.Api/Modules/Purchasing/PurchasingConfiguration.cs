using Microsoft.EntityFrameworkCore;
using Wros.Api.Shared.Operations;
namespace Wros.Api.Modules.Purchasing;
public static class PurchasingConfiguration
{
    public static void Apply(ModelBuilder model)
    {
        model.Entity<DocumentCounter>().HasKey(x => x.Key);
        var order = model.Entity<PurchaseOrder>(); OperationRules.Configure(order); order.HasIndex(x => x.Number).IsUnique();
        order.HasIndex(x => new { x.OrderDate, x.Id }); order.HasIndex(x => new { x.Status, x.OrderDate });
        order.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        order.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        order.HasMany(x => x.Revisions).WithOne().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        var line = model.Entity<PurchaseOrderLine>(); line.HasKey(x => x.Id); line.Property(x => x.Quantity).HasPrecision(18, 0);
        line.HasIndex(x => new { x.PurchaseOrderId, x.Position }).IsUnique();
        line.HasOne<SupplierCatalog.SupplierProduct>().WithMany().HasForeignKey(x => x.SupplierProductId).OnDelete(DeleteBehavior.Restrict);
        line.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        line.ToTable(t => {
            t.HasCheckConstraint("CK_PurchaseOrderLine_Quantity", "[Quantity]>0");
            t.HasCheckConstraint("CK_PurchaseOrderLine_Identity", "[ProductId] IS NOT NULL OR ([SupplierProductId] IS NOT NULL AND [PricingJson] IS NOT NULL)");
        });
        var revision = model.Entity<PurchaseOrderRevision>(); revision.HasKey(x => x.Id);
        revision.HasIndex(x => new { x.PurchaseOrderId, x.Revision }).IsUnique(); revision.Property(x => x.Timestamp).HasPrecision(3);
    }
}
