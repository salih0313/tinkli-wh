using System.Data;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Audit;
using Wros.Api.Modules.Purchasing;
using Wros.Api.Modules.Warehouses;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Operations;
using Wros.Api.Shared.Persistence;
namespace Wros.Api.Modules.Receiving;
public sealed partial class ReceivingService(WrosDbContext db, AuditService audit, DocumentNumbers numbers,
    PurchaseOrderService orders, ReceiptQueries queries)
{
    public async Task<ReceiptDto> Save(Guid? id, ReceiptRequest request)
    {
        if (request.ReceivedAt == default || request.Lines is null || request.Lines.Length > 500)
            throw new ApiProblemException(400, "Choose a received date/time and at most 500 product lines.");
        if (request.Lines.Any(line => line is null || line.Lots is null || line.Lots.Any(lot => lot is null || lot.Allocations is null || lot.Allocations.Any(a => a is null))))
            throw new ApiProblemException(400, "Product lines, lots and allocations must contain valid objects.");
        if (request.Lines.Sum(x => x.Lots.Sum(l => l.Allocations.Length)) > 5000) throw new ApiProblemException(400, "Use at most 5,000 allocations per document.");
        foreach (var line in request.Lines) { PurchaseOrderService.Quantity(line.Quantity, true); foreach (var lot in line.Lots) { PurchaseOrderService.Quantity(lot.Quantity, true); DistinctAllocations(lot.Allocations); } }
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var note = id.HasValue ? await queries.Graph().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiProblemException(404, "Delivery note not found.") : new DeliveryNote();
        if (id.HasValue)
        {
            OperationRules.CheckVersion(note, request.RowVersion);
            if (note.Status != "Draft") throw new ApiProblemException(409, "Posted receipts cannot be edited. Use allocation correction.");
            if (note.PurchaseOrderId != request.PurchaseOrderId) throw new ApiProblemException(400, "A delivery note's PO link cannot be changed.");
        }
        var before = id.HasValue ? SafeDraft(note) : null;
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == request.SupplierId && x.IsActive) ?? throw new ApiProblemException(400, "Choose an active supplier.");
        PurchaseOrder? po = null;
        if (request.PurchaseOrderId.HasValue)
        {
            po = await db.PurchaseOrders.Include(x => x.Supplier).Include(x => x.Lines).ThenInclude(x => x.Product).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == request.PurchaseOrderId)
                ?? throw new ApiProblemException(400, "Purchase order not found.");
            if (po.SupplierId != supplier.Id) throw new ApiProblemException(400, "The supplier must match the linked purchase order.");
        }
        var productIds = request.Lines.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products.Where(x => productIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id);
        if (products.Count != productIds.Length) throw new ApiProblemException(400, "Choose active products for every receipt line.");
        await ValidatePlacements(request.Lines.SelectMany(x => x.Lots).SelectMany(x => x.Allocations));
        if (id.HasValue)
        {
            var lots = note.Lines.SelectMany(x => x.Lots).ToArray();
            db.DeliveryNoteAllocations.RemoveRange(lots.SelectMany(x => x.Allocations).ToArray()); db.StockLots.RemoveRange(lots); db.DeliveryNoteLines.RemoveRange(note.Lines);
            note.Lines.Clear(); await db.SaveChangesAsync();
        }
        else { note.Number = await numbers.Next("DN"); note.CreatedBy = audit.ActorId; db.DeliveryNotes.Add(note); }
        if (po is not null) orders.LockForReceiving(po);
        note.Supplier = supplier; note.SupplierId = supplier.Id; note.PurchaseOrder = po; note.PurchaseOrderId = po?.Id;
        note.SupplierReference = OperationRules.Optional(request.SupplierReference, 100, "Supplier delivery reference");
        note.ReceivedAt = request.ReceivedAt.ToUniversalTime(); note.Remarks = OperationRules.Optional(request.Remarks, 2000, "Remarks"); Touch(note);
        foreach (var (line, index) in request.Lines.Select((value, index) => (value, index)))
        {
            var entity = new DeliveryNoteLine { DeliveryNoteId = note.Id, ProductId = line.ProductId, Quantity = line.Quantity, Position = index };
            note.Lines.Add(entity); db.DeliveryNoteLines.Add(entity);
            foreach (var lot in line.Lots)
            {
                var group = new StockLot { DeliveryNoteLineId = entity.Id, ProductId = line.ProductId, Quantity = lot.Quantity,
                    SupplierBatch = OperationRules.Optional(lot.SupplierBatch, 100, "Supplier batch"), Expiry = lot.Expiry, ReceivedAt = note.ReceivedAt };
                entity.Lots.Add(group); db.StockLots.Add(group);
                foreach (var a in lot.Allocations) { var allocation = new DeliveryNoteAllocation { StockLotId = group.Id, RackId = a.RackId, LocationId = a.LocationId, Quantity = a.Quantity }; group.Allocations.Add(allocation); db.DeliveryNoteAllocations.Add(allocation); }
            }
        }
        audit.Record(id.HasValue ? "DeliveryNoteDraftUpdated" : "DeliveryNoteCreated", "DeliveryNote", note.Id.ToString(), before, SafeDraft(note));
        await db.SaveChangesAsync(); await tx.CommitAsync(); return await queries.Get(note.Id);
    }
    private static object SafeDraft(DeliveryNote note) => new
    {
        note.Number, note.SupplierId, note.PurchaseOrderId, note.ReceivedAt, note.Status,
        Lines = note.Lines.OrderBy(x => x.Position).Select(line => new
        {
            line.ProductId, line.Quantity,
            Lots = line.Lots.Select(lot => new { lot.Id, lot.SupplierBatch, lot.Expiry, lot.Quantity,
                Allocations = lot.Allocations.Select(a => new { a.RackId, a.LocationId, a.Quantity }).ToArray() }).ToArray()
        }).ToArray()
    };
    private void Touch(DeliveryNote note) { note.UpdatedAt = DateTimeOffset.UtcNow; note.UpdatedBy = audit.ActorId; }
    private async Task<Dictionary<(Guid, Guid?), Guid>> ValidatePlacements(IEnumerable<AllocationRequest> source)
    {
        var allocations = source.ToArray();
        foreach (var allocation in allocations) PurchaseOrderService.Quantity(allocation.Quantity, false);
        var rackIds = allocations.Select(x => x.RackId).Distinct().ToArray();
        var racks = await db.StorageRacks.Include(x => x.Area).ThenInclude(x => x.Warehouse).Where(x => rackIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var locationIds = allocations.Where(x => x.LocationId.HasValue).Select(x => x.LocationId!.Value).Distinct().ToArray();
        var locations = await db.StorageLocations.Where(x => locationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var result = new Dictionary<(Guid, Guid?), Guid>();
        foreach (var a in allocations)
        {
            if (!racks.TryGetValue(a.RackId, out var rack) || !rack.IsActive || !rack.Area.IsActive || !rack.Area.Warehouse.IsActive)
                throw new ApiProblemException(400, "Choose active racks with active warehouse/area ancestors.");
            if (a.LocationId.HasValue && (!locations.TryGetValue(a.LocationId.Value, out var location) || !location.IsActive || location.RackId != a.RackId))
                throw new ApiProblemException(400, "Choose an active location belonging to the selected rack.");
            result[(a.RackId, a.LocationId)] = rack.Area.WarehouseId;
        }
        return result;
    }
    private static void DistinctAllocations(IEnumerable<AllocationRequest> allocations)
    {
        var all = allocations.ToArray();
        if (all.Select(x => (x.RackId, x.LocationId)).Distinct().Count() != all.Length) throw new ApiProblemException(400, "Combine duplicate rack/location entries within a lot.");
    }
}
