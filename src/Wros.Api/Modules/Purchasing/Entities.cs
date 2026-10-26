using System.ComponentModel.DataAnnotations;
using Wros.Api.Modules.Catalog;
using Wros.Api.Modules.Suppliers;
using Wros.Api.Shared.Operations;
namespace Wros.Api.Modules.Purchasing;
public sealed class PurchaseOrder : OperationalRecord
{
    [MaxLength(32)] public string Number { get; set; } = "";
    public Guid SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public DateOnly OrderDate { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }
    [MaxLength(2000)] public string? Remarks { get; set; }
    [MaxLength(16)] public string Status { get; set; } = "Draft";
    public int Revision { get; set; }
    public List<PurchaseOrderLine> Lines { get; set; } = [];
    public List<PurchaseOrderRevision> Revisions { get; set; } = [];
}
public sealed class PurchaseOrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseOrderId { get; set; }
    public Guid? ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid? SupplierProductId { get; set; }
    public string? PricingJson { get; set; }
    public int Position { get; set; }
    public decimal Quantity { get; set; }
}
public sealed class PurchaseOrderRevision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PurchaseOrderId { get; set; }
    public int Revision { get; set; }
    [MaxLength(32)] public string Action { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public Guid? ActorId { get; set; }
    public string Snapshot { get; set; } = "";
}
