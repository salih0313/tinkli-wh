using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Inventory;
using Wros.Api.Modules.Warehouses;
using Wros.Api.Shared.Operations;
namespace Wros.Api.Modules.Receiving;
public static class ReceivingConfiguration
{
    public static void Apply(ModelBuilder model)
    {
        var note = model.Entity<DeliveryNote>(); OperationRules.Configure(note);
        note.HasIndex(x => x.Number).IsUnique(); note.HasIndex(x => new { x.Status, x.ReceivedAt }); note.HasIndex(x => new { x.ReceivedAt, x.Id });
        note.Property(x => x.ReceivedAt).HasPrecision(3); note.Property(x => x.PostedAt).HasPrecision(3);
        note.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        note.HasOne(x => x.PurchaseOrder).WithMany().HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
        note.HasMany(x => x.Lines).WithOne(x => x.DeliveryNote).HasForeignKey(x => x.DeliveryNoteId).OnDelete(DeleteBehavior.Restrict);
        var line = model.Entity<DeliveryNoteLine>(); line.HasKey(x => x.Id); line.Property(x => x.Quantity).HasPrecision(18, 0);
        line.HasIndex(x => new { x.DeliveryNoteId, x.Position }).IsUnique();
        line.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        line.HasMany(x => x.Lots).WithOne(x => x.DeliveryNoteLine).HasForeignKey(x => x.DeliveryNoteLineId).OnDelete(DeleteBehavior.Restrict);
        line.ToTable(t => t.HasCheckConstraint("CK_DeliveryNoteLine_Quantity", "[Quantity]>=0"));
        var lot = model.Entity<StockLot>(); lot.HasKey(x => x.Id); lot.Property(x => x.Quantity).HasPrecision(18, 0); lot.Property(x => x.ReceivedAt).HasPrecision(3);
        lot.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        lot.HasIndex(x => new { x.ProductId, x.ReceivedAt }); lot.HasIndex(x => x.Expiry);
        lot.HasMany(x => x.Allocations).WithOne(x => x.StockLot).HasForeignKey(x => x.StockLotId).OnDelete(DeleteBehavior.Restrict);
        lot.HasMany(x => x.Placements).WithOne().HasForeignKey(x => x.StockLotId).OnDelete(DeleteBehavior.Restrict);
        lot.ToTable(t => t.HasCheckConstraint("CK_StockLot_Quantity", "[Quantity]>=0"));
        var allocation = model.Entity<DeliveryNoteAllocation>(); allocation.HasKey(x => x.Id); allocation.Property(x => x.Quantity).HasPrecision(18, 0);
        allocation.HasOne(x => x.Rack).WithMany().HasForeignKey(x => x.RackId).OnDelete(DeleteBehavior.Restrict);
        allocation.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        allocation.HasIndex(x => new { x.StockLotId, x.RackId, x.LocationId }).IsUnique().HasFilter(null);
        allocation.ToTable(t => t.HasCheckConstraint("CK_ReceiptAllocation_Quantity", "[Quantity]>0"));
        var placement = model.Entity<LotPlacement>(); placement.HasKey(x => x.Id); placement.Property(x => x.Quantity).HasPrecision(18, 0);
        placement.HasOne<StorageRack>().WithMany().HasForeignKey(x => x.RackId).OnDelete(DeleteBehavior.Restrict);
        placement.HasOne<StorageLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        placement.HasIndex(x => new { x.StockLotId, x.RackId, x.LocationId }).IsUnique().HasFilter(null);
        placement.ToTable(t => t.HasCheckConstraint("CK_LotPlacement_Quantity", "[Quantity]>0"));
        var balance = model.Entity<InventoryBalance>(); OperationRules.Configure(balance); balance.Property(x => x.Quantity).HasPrecision(18, 0);
        balance.HasOne(x => x.StockLot).WithMany().HasForeignKey(x => x.StockLotId).OnDelete(DeleteBehavior.Restrict);
        balance.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        balance.HasOne(x => x.Rack).WithMany().HasForeignKey(x => x.RackId).OnDelete(DeleteBehavior.Restrict);
        balance.HasOne(x => x.Location).WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        balance.HasIndex(x => new { x.StockLotId, x.RackId, x.LocationId }).IsUnique().HasFilter(null);
        balance.HasIndex(x => new { x.WarehouseId, x.RackId, x.LocationId });
        balance.ToTable(t => t.HasCheckConstraint("CK_InventoryBalance_Nonnegative", "[Quantity]>=0"));
        var movement = model.Entity<StockMovement>(); movement.HasKey(x => x.Id); movement.Property(x => x.Quantity).HasPrecision(18, 0); movement.Property(x => x.Timestamp).HasPrecision(3);
        movement.HasOne(x => x.StockLot).WithMany().HasForeignKey(x => x.StockLotId).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne(x => x.DeliveryNote).WithMany().HasForeignKey(x => x.DeliveryNoteId).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne<Warehouse>().WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne<StorageRack>().WithMany().HasForeignKey(x => x.RackId).OnDelete(DeleteBehavior.Restrict);
        movement.HasOne<StorageLocation>().WithMany().HasForeignKey(x => x.LocationId).OnDelete(DeleteBehavior.Restrict);
        movement.HasIndex(x => new { x.StockLotId, x.Timestamp, x.Id }); movement.HasIndex(x => new { x.DeliveryNoteId, x.Timestamp, x.Id });
        movement.HasIndex(x => x.CorrectionId); movement.HasIndex(x => new { x.Timestamp, x.Id }); movement.HasIndex(x => new { x.ReferenceType, x.ReferenceId });
        movement.ToTable(t => t.HasCheckConstraint("CK_StockMovement_Sign", "([Type] IN ('Receipt','RelocationIn','TransferIn') AND [Quantity]>0) OR ([Type] IN ('RelocationOut','SupplierReturn','Disposal','TransferOut','ExternalOrder') AND [Quantity]<0)"));
    }
}
