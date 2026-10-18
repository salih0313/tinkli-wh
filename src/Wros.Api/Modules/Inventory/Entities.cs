using System.ComponentModel.DataAnnotations;
using Wros.Api.Modules.Receiving;
using Wros.Api.Modules.Warehouses;
using Wros.Api.Shared.Operations;
namespace Wros.Api.Modules.Inventory;
public sealed class InventoryBalance : OperationalRecord
{
    public Guid StockLotId { get; set; }
    public StockLot StockLot { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Warehouse Warehouse { get; set; } = null!;
    public Guid RackId { get; set; }
    public StorageRack Rack { get; set; } = null!;
    public Guid? LocationId { get; set; }
    public StorageLocation? Location { get; set; }
    public decimal Quantity { get; set; }
}
public sealed class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DeliveryNoteId { get; set; }
    public DeliveryNote DeliveryNote { get; set; } = null!;
    public Guid StockLotId { get; set; }
    public StockLot StockLot { get; set; } = null!;
    public Guid WarehouseId { get; set; }
    public Guid RackId { get; set; }
    public Guid? LocationId { get; set; }
    [MaxLength(32)] public string Type { get; set; } = "";
    public decimal Quantity { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ActorId { get; set; }
    [MaxLength(80)] public string CorrelationId { get; set; } = "";
    public Guid? CorrectionId { get; set; }
}
