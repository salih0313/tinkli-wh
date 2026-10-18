using System.ComponentModel.DataAnnotations;
namespace Wros.Api.Modules.Receiving;
public sealed record AllocationRequest(Guid RackId, Guid? LocationId, decimal Quantity);
public sealed record LotRequest([MaxLength(100)] string? SupplierBatch, DateOnly? Expiry, decimal Quantity,
    [Required, MaxLength(500)] AllocationRequest[] Allocations);
public sealed record ReceiptLineRequest(Guid ProductId, decimal Quantity, [Required, MaxLength(100)] LotRequest[] Lots);
public sealed record ReceiptRequest(Guid SupplierId, Guid? PurchaseOrderId, [MaxLength(100)] string? SupplierReference,
    DateTimeOffset ReceivedAt, [MaxLength(2000)] string? Remarks, [Required, MaxLength(500)] ReceiptLineRequest[] Lines, string? RowVersion);
public sealed record LotCorrection(Guid StockLotId, [Required, MaxLength(500)] AllocationRequest[] Allocations);
public sealed record CorrectionRequest([Required] string RowVersion, [Required, MinLength(1), MaxLength(50000)] LotCorrection[] Lots);
public sealed record AllocationDto(Guid RackId, Guid? LocationId, string Path, decimal Quantity);
public sealed record LotDto(Guid Id, string? SupplierBatch, DateOnly? Expiry, decimal Quantity, DateTimeOffset ReceivedAt,
    AllocationDto[] Allocations, AllocationDto[] Placements, BalanceDto[] Balances);
public sealed record ReceiptLineDto(Guid Id, Guid ProductId, string Sku, string Name, bool TrackExpiry, decimal Quantity, LotDto[] Lots);
public sealed record ReceiptDto(Guid Id, string Number, Guid SupplierId, string SupplierCode, string SupplierName, Guid? PurchaseOrderId,
    string? PurchaseOrderNumber, string? SupplierReference, DateTimeOffset ReceivedAt, string? Remarks, string Status,
    DateTimeOffset? PostedAt, Guid? PostedBy, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Guid? CreatedBy, Guid? UpdatedBy,
    string RowVersion, ReceiptLineDto[] Lines);
public sealed record ReceiptSummary(Guid Id, string Number, string SupplierName, string? PurchaseOrderNumber, DateTimeOffset ReceivedAt, string Status);
public sealed record PlacementDto(Guid RackId, Guid? LocationId, Guid WarehouseId, string Path);
public sealed record BalanceDto(Guid Id, Guid DeliveryNoteId, string DeliveryNoteNumber, Guid ProductId, string Sku, string Product,
    Guid StockLotId, string? SupplierBatch, DateOnly? Expiry, DateTimeOffset ReceivedAt, Guid WarehouseId, string Warehouse,
    Guid RackId, Guid? LocationId, string Path, decimal Quantity, decimal SellableQuantity);
public sealed record MovementDto(Guid Id, Guid StockLotId, Guid WarehouseId, Guid RackId, Guid? LocationId, string Type, decimal Quantity,
    DateTimeOffset Timestamp, Guid? ActorId, string CorrelationId, Guid? CorrectionId);

public sealed record ReceiptOrderLookup(Guid Id, string Number, string SupplierName, string Status);
