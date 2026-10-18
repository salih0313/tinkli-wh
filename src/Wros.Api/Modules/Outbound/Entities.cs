using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Wros.Api.Modules.Inventory;
using Wros.Api.Modules.Suppliers;
using Wros.Api.Modules.Receiving;
using Wros.Api.Modules.Purchasing;
using Wros.Api.Modules.Warehouses;
namespace Wros.Api.Modules.Outbound;
// Completed physical transactions only: no pending stock or approval workflow.
public sealed class StockTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    [MaxLength(32)] public string Number { get; set; } = "";
    [MaxLength(20)] public string Kind { get; set; } = "";
    public Guid IdempotencyKey { get; set; }
    [MaxLength(64)] public string RequestHash { get; set; } = "";
    public Guid? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public Guid? DeliveryNoteId { get; set; }
    public Guid? PurchaseOrderId { get; set; }
    [MaxLength(2000)]
    public string? Remarks { get; set; }
    public DateTimeOffset CompletedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid ActorId { get; set; }
    [MaxLength(80)] public string CorrelationId { get; set; } = "";
    public List<StockTransactionLine> Lines { get; set; } = [];
}
public sealed class StockTransactionLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockTransactionId { get; set; }
    public Guid SourceBalanceId { get; set; }
    public InventoryBalance SourceBalance { get; set; } = null!;
    public decimal Quantity { get; set; }
    public Guid? DestinationRackId { get; set; }
    public StorageRack? DestinationRack { get; set; }
    public Guid? DestinationLocationId { get; set; }
    public StorageLocation? DestinationLocation { get; set; }
}
public static class OutboundConfiguration
{
    public static void Apply(Microsoft.EntityFrameworkCore.ModelBuilder m)
    {
        var t = m.Entity<StockTransaction>();
        t.HasKey(x => x.Id);
        t.HasIndex(x => x.Number).IsUnique();
        t.HasIndex(x => x.IdempotencyKey).IsUnique();
        t.HasIndex(x => new { x.Kind, x.CompletedAt, x.Id });
        t.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        t.HasOne<DeliveryNote>().WithMany().HasForeignKey(x => x.DeliveryNoteId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        t.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        t.HasMany(x => x.Lines).WithOne().HasForeignKey(x => x.StockTransactionId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        var l = m.Entity<StockTransactionLine>();
        l.HasKey(x => x.Id);
        l.Property(x => x.Quantity).HasPrecision(18, 0);
        l.HasOne(x => x.SourceBalance).WithMany().HasForeignKey(x => x.SourceBalanceId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        l.HasOne(x => x.DestinationRack).WithMany().HasForeignKey(x => x.DestinationRackId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        l.HasOne(x => x.DestinationLocation).WithMany().HasForeignKey(x => x.DestinationLocationId).OnDelete(Microsoft.EntityFrameworkCore.DeleteBehavior.Restrict);
        l.ToTable(t => t.HasCheckConstraint("CK_StockTransactionLine_Positive", "[Quantity]>0"));
        t.ToTable(t => t.HasCheckConstraint("CK_StockTransaction_Kind", "[Kind] IN ('SupplierReturn','Disposal','Transfer')"));
    }
}
