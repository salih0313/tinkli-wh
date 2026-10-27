using System.Data;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Inventory;
using Wros.Api.Modules.Purchasing;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Operations;
namespace Wros.Api.Modules.Receiving;
public sealed partial class ReceivingService
{
    public async Task<ReceiptDto> Post(Guid id, VersionRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var note = await queries.Graph().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiProblemException(404, "Delivery note not found.");
        OperationRules.CheckVersion(note, request.RowVersion);
        if (note.Status != "Draft") throw new ApiProblemException(409, "This receipt is already posted. Stock will not be added again.");
        if (!note.Supplier.IsActive) throw new ApiProblemException(400, "Supplier must be active before posting.");
        var allocations = note.Lines.SelectMany(x => x.Lots).SelectMany(x => x.Allocations)
            .Select(x => new AllocationRequest(x.RackId, x.LocationId, x.Quantity)).ToArray();
        var warehouses = await ValidatePlacements(allocations);
        foreach (var line in note.Lines)
        {
            if (!line.Product.IsActive) throw new ApiProblemException(400, "Every received product must be active.");
            PurchaseOrderService.Quantity(line.Quantity, true);
            if (line.Lots.Sum(x => x.Quantity) != line.Quantity) throw new ApiProblemException(400, $"Lot quantities must equal received quantity for {line.Product.Code}.");
            foreach (var lot in line.Lots)
            {
                PurchaseOrderService.Quantity(lot.Quantity, true);
                if (lot.Quantity > 0 && line.Product.TrackExpiry && lot.Expiry is null) throw new ApiProblemException(400, $"Expiry is required for {line.Product.Code}.");
                var allocated = lot.Allocations.Sum(x => x.Quantity);
                if (allocated != lot.Quantity)
                    throw new ApiProblemException(400, FormattableString.Invariant(
                        $"Cannot post delivery note. Product {line.Product.Code} ({line.Product.Name}), lot {line.Lots.IndexOf(lot) + 1}: received {lot.Quantity:0} units, allocated {allocated:0} units. Edit the draft and adjust rack/location allocations to total exactly {lot.Quantity:0} units for this lot, then save and post again. No stock was added."));
                foreach (var allocation in lot.Allocations)
                {
                    var warehouseId = warehouses[(allocation.RackId, allocation.LocationId)];
                    var balance = new InventoryBalance { StockLotId = lot.Id, WarehouseId = warehouseId, RackId = allocation.RackId,
                        LocationId = allocation.LocationId, Quantity = allocation.Quantity, CreatedBy = audit.ActorId, UpdatedBy = audit.ActorId };
                    db.InventoryBalances.Add(balance);
                    db.LotPlacements.Add(new LotPlacement { StockLotId = lot.Id, RackId = allocation.RackId, LocationId = allocation.LocationId, Quantity = allocation.Quantity });
                    Movement(note.Id, lot.Id, warehouseId, allocation.RackId, allocation.LocationId, "Receipt", allocation.Quantity, null);
                }
            }
        }
        note.Status = "Posted"; note.PostedAt = DateTimeOffset.UtcNow; note.PostedBy = audit.ActorId; Touch(note);
        audit.Record("DeliveryNotePosted", "DeliveryNote", id.ToString(), new { Status = "Draft" }, new { note.Number, note.Status, note.PostedAt });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return await queries.Get(id);
    }
    public async Task<ReceiptDto> Correct(Guid id, CorrectionRequest request)
    {
        if (request.Lots is null || request.Lots.Length == 0 || request.Lots.Any(l => l is null || l.Allocations is null || l.Allocations.Any(a => a is null)))
            throw new ApiProblemException(400, "Choose valid lot corrections and allocation objects.");
        if (request.Lots.Select(x => x.StockLotId).Distinct().Count() != request.Lots.Length || request.Lots.Sum(x => x.Allocations.Length) > 5000)
            throw new ApiProblemException(400, "Choose distinct receipt lots and at most 5,000 allocation entries.");
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var note = await queries.Graph().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiProblemException(404, "Delivery note not found.");
        OperationRules.CheckVersion(note, request.RowVersion);
        if (note.Status != "Posted") throw new ApiProblemException(409, "Only posted receipts can use allocation correction.");
        var lots = note.Lines.SelectMany(x => x.Lots).ToDictionary(x => x.Id);
        if (request.Lots.Any(x => !lots.ContainsKey(x.StockLotId))) throw new ApiProblemException(400, "Choose lots belonging to this delivery note.");
        foreach (var change in request.Lots) foreach (var allocation in change.Allocations) PurchaseOrderService.Quantity(allocation.Quantity, false);
        // Unchanged nominal assignments may anchor already-consumed stock at an inactive empty location.
        // Only destinations receiving an additional physical quantity need to be currently active.
        var addedDestinations = request.Lots.SelectMany(change => change.Allocations.Where(a =>
            a.Quantity > lots[change.StockLotId].Placements.Where(p => p.RackId == a.RackId && p.LocationId == a.LocationId).Sum(p => p.Quantity)));
        var warehouses = await ValidatePlacements(addedDestinations);
        var lotIds = request.Lots.Select(x => x.StockLotId).ToArray();
        var balances = await db.InventoryBalances.Where(x => lotIds.Contains(x.StockLotId)).OrderBy(x => x.StockLotId).ThenBy(x => x.RackId).ThenBy(x => x.LocationId).ToListAsync();
        var oldRackIds = request.Lots.SelectMany(x => lots[x.StockLotId].Placements).Select(x => x.RackId).Distinct().ToArray();
        var oldWarehouses = await db.StorageRacks.Where(x => oldRackIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Area.WarehouseId);
        var before = request.Lots.Select(x => new { x.StockLotId, Allocations = lots[x.StockLotId].Placements.Select(p => new { p.RackId, p.LocationId, p.Quantity }).ToArray() }).ToArray();
        var correctionId = Guid.NewGuid();
        foreach (var change in request.Lots)
        {
            var lot = lots[change.StockLotId]; DistinctAllocations(change.Allocations);
            if (change.Allocations.Sum(x => x.Quantity) != lot.Quantity) throw new ApiProblemException(400, "Corrected allocations must preserve the lot's full original received quantity.");
            var old = lot.Placements.ToDictionary(x => (x.RackId, x.LocationId), x => x.Quantity);
            var target = change.Allocations.ToDictionary(x => (x.RackId, x.LocationId), x => x.Quantity);
            var warehouseDeltas = old.Keys.Concat(target.Keys).Distinct()
                .GroupBy(key => oldWarehouses.TryGetValue(key.RackId, out var warehouse) ? warehouse : warehouses[key])
                .Select(group => group.Sum(key => target.GetValueOrDefault(key) - old.GetValueOrDefault(key)));
            if (warehouseDeltas.Any(delta => delta != 0))
                throw new ApiProblemException(400, "Rack corrections must preserve each warehouse's quantity. Use Warehouse transfers for completed interwarehouse moves.");
            foreach (var key in old.Keys.Concat(target.Keys).Distinct().OrderBy(x => x.RackId).ThenBy(x => x.LocationId))
            {
                var delta = target.GetValueOrDefault(key) - old.GetValueOrDefault(key);
                if (delta == 0) continue;
                var balance = balances.SingleOrDefault(x => x.StockLotId == lot.Id && x.RackId == key.RackId && x.LocationId == key.LocationId);
                if (delta < 0 && (balance is null || balance.Quantity < -delta))
                    throw new ApiProblemException(409, "Source stock is no longer available. Consumed stock cannot be relocated; refresh and reduce the correction.");
                var warehouseId = balance?.WarehouseId ?? warehouses[key];
                if (balance is null)
                {
                    balance = new InventoryBalance { StockLotId = lot.Id, WarehouseId = warehouseId, RackId = key.RackId, LocationId = key.LocationId, CreatedBy = audit.ActorId };
                    db.InventoryBalances.Add(balance); balances.Add(balance);
                }
                balance.Quantity += delta; balance.UpdatedAt = DateTimeOffset.UtcNow; balance.UpdatedBy = audit.ActorId;
                Movement(note.Id, lot.Id, warehouseId, key.RackId, key.LocationId, delta < 0 ? "RelocationOut" : "RelocationIn", delta, correctionId);
            }
            db.LotPlacements.RemoveRange(lot.Placements.ToArray());
        }
        // Flush removed current-placement keys before replacement; original allocations are never changed.
        await db.SaveChangesAsync();
        foreach (var change in request.Lots)
            db.LotPlacements.AddRange(change.Allocations.Select(a => new LotPlacement { StockLotId = change.StockLotId, RackId = a.RackId, LocationId = a.LocationId, Quantity = a.Quantity }));
        Touch(note);
        audit.Record("DeliveryNoteAllocationCorrected", "DeliveryNote", id.ToString(), before,
            new { CorrectionId = correctionId, Lots = request.Lots.Select(x => new { x.StockLotId, x.Allocations }) });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return await queries.Get(id);
    }
    private void Movement(Guid noteId, Guid lotId, Guid warehouseId, Guid rackId, Guid? locationId, string type, decimal quantity, Guid? correctionId)
        => db.StockMovements.Add(new StockMovement { DeliveryNoteId = noteId, StockLotId = lotId, WarehouseId = warehouseId,
            RackId = rackId, LocationId = locationId, Type = type, Quantity = quantity, ActorId = audit.ActorId, CorrelationId = audit.CorrelationId, CorrectionId = correctionId });
}
