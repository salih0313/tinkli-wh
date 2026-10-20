using System.ComponentModel.DataAnnotations;
namespace Wros.Api.Modules.Purchasing;
public sealed record OrderLineRequest(Guid ProductId, decimal Quantity, Guid? SupplierProductId = null, Guid? OriginalLineId = null, bool RefreshPrice = false, string? CatalogRowVersion = null);
public sealed record OrderRequest(Guid SupplierId, DateOnly OrderDate, DateOnly? ExpectedDeliveryDate,
    [MaxLength(2000)] string? Remarks, [Required, MinLength(1), MaxLength(500)] OrderLineRequest[] Lines, string? RowVersion);
public sealed record VersionRequest([Required] string RowVersion);
public sealed record OrderLineDto(Guid Id, Guid ProductId, string Sku, string Name, decimal Quantity, Guid? SupplierProductId, SupplierCatalog.PricingSnapshot? Pricing);
public sealed record OrderDto(Guid Id, string Number, Guid SupplierId, string SupplierCode, string SupplierName,
    DateOnly OrderDate, DateOnly? ExpectedDeliveryDate, string? Remarks, string Status, int Revision, string RowVersion,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Guid? CreatedBy, Guid? UpdatedBy, OrderLineDto[] Lines, SupplierCatalog.PricingSummary Summary);
public sealed record RevisionDto(int Revision, string Action, DateTimeOffset Timestamp, Guid? ActorId, string Snapshot);
