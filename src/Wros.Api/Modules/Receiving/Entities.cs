using System.ComponentModel.DataAnnotations;
using Wros.Api.Modules.Catalog;
using Wros.Api.Modules.Purchasing;
using Wros.Api.Modules.Suppliers;
using Wros.Api.Modules.Warehouses;
using Wros.Api.Shared.Operations;
namespace Wros.Api.Modules.Receiving;
public sealed class DeliveryNote : OperationalRecord
{
    [MaxLength(32)] public string Number { get; set; } = "";
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public Guid? PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }
    [MaxLength(100)] public string? SupplierReference { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    [MaxLength(2000)] public string? Remarks { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "Draft";
    public DateTimeOffset? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public List<DeliveryNoteLine> Lines { get; set; } = [];
}
public sealed class DeliveryNoteLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeliveryNoteId { get; set; }
    public DeliveryNote DeliveryNote { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int Position { get; set; }
    public decimal Quantity { get; set; }
    public List<StockLot> Lots { get; set; } = [];
}
public sealed class StockLot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeliveryNoteLineId { get; set; }
    public DeliveryNoteLine DeliveryNoteLine { get; set; } = null!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    [MaxLength(100)] public string? SupplierBatch { get; set; }
    public DateOnly? Expiry { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public decimal Quantity { get; set; }
    public List<DeliveryNoteAllocation> Allocations { get; set; } = [];
    public List<LotPlacement> Placements { get; set; } = [];
}
public sealed class DeliveryNoteAllocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockLotId { get; set; }
    public StockLot StockLot { get; set; } = null!;
    public Guid RackId { get; set; }
    public StorageRack Rack { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public StorageLocation? Location { get; set; }
    public decimal Quantity { get; set; }
}
// Current nominal assignment of the original receipt quantity, separate from immutable receipt allocation.
public sealed class LotPlacement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StockLotId { get; set; }
    public Guid RackId { get; set; }
    public Guid? LocationId { get; set; }
    public decimal Quantity { get; set; }
}
