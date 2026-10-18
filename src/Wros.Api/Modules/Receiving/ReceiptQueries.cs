using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Identity;
using Wros.Api.Modules.Inventory;
using Wros.Api.Modules.Warehouses;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Operations;
using Wros.Api.Shared.Persistence;
namespace Wros.Api.Modules.Receiving;
public sealed class ReceiptQueries(WrosDbContext db)
{
    public IQueryable<DeliveryNote> Graph() => db.DeliveryNotes.Include(x => x.Supplier).Include(x => x.PurchaseOrder)
        .Include(x => x.Lines).ThenInclude(x => x.Product)
        .Include(x => x.Lines).ThenInclude(x => x.Lots).ThenInclude(x => x.Allocations).ThenInclude(x => x.Rack).ThenInclude(x => x.Area).ThenInclude(x => x.Warehouse)
        .Include(x => x.Lines).ThenInclude(x => x.Lots).ThenInclude(x => x.Allocations).ThenInclude(x => x.Location)
        .Include(x => x.Lines).ThenInclude(x => x.Lots).ThenInclude(x => x.Placements).AsSplitQuery();
    private IQueryable<InventoryBalance> Balances() => db.InventoryBalances.AsNoTracking().Include(x => x.Warehouse)
        .Include(x => x.Rack).ThenInclude(x => x.Area).ThenInclude(x => x.Warehouse).Include(x => x.Location)
        .Include(x => x.StockLot).ThenInclude(x => x.Product)
        .Include(x => x.StockLot).ThenInclude(x => x.DeliveryNoteLine).ThenInclude(x => x.DeliveryNote);
    public async Task<PageDto<ReceiptOrderLookup>> PurchaseOrders(int page, int pageSize)
    {
        var query = db.PurchaseOrders.AsNoTracking(); var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ReceiptOrderLookup(x.Id, x.Number, x.Supplier.Name, x.Status)).ToArrayAsync();
        return new(items, page, pageSize, total);
    }
    public async Task<PageDto<ReceiptSummary>> List(int page, int pageSize, string? search, string? status)
    {
        var query = db.DeliveryNotes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = OperationRules.Required(search, 100, "Search"); query = query.Where(x => x.Number.Contains(term) || x.Supplier.Name.Contains(term) || (x.SupplierReference != null && x.SupplierReference.Contains(term))); }
        if (!string.IsNullOrEmpty(status))
        {
            if (status is not ("Draft" or "Posted")) throw new ApiProblemException(400, "Choose Draft or Posted.");
            query = query.Where(x => x.Status == status);
        }
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ReceiptSummary(x.Id, x.Number, x.Supplier.Name, x.PurchaseOrder == null ? null : x.PurchaseOrder.Number, x.ReceivedAt, x.Status)).ToArrayAsync();
        return new(items, page, pageSize, total);
    }
    public async Task<ReceiptDto> Get(Guid id)
    {
        var x = await Graph().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiProblemException(404, "Delivery note not found.");
        var balances = await Balances().Where(b => b.StockLot.DeliveryNoteLine.DeliveryNoteId == id).ToListAsync();
        var paths = await PlacementPaths(x.Lines.SelectMany(l => l.Lots).SelectMany(l => l.Placements).Select(p => (p.RackId, p.LocationId)));
        return new(x.Id, x.Number, x.SupplierId, x.Supplier.Code, x.Supplier.Name, x.PurchaseOrderId, x.PurchaseOrder?.Number,
            x.SupplierReference, x.ReceivedAt, x.Remarks, x.Status, x.PostedAt, x.PostedBy, x.CreatedAt, x.UpdatedAt, x.CreatedBy, x.UpdatedBy, Convert.ToBase64String(x.RowVersion),
            x.Lines.OrderBy(l => l.Position).Select(l => new ReceiptLineDto(l.Id, l.ProductId, l.Product.Code, l.Product.Name, l.Product.TrackExpiry, l.Quantity,
                l.Lots.OrderBy(lot => lot.Id).Select(lot => new LotDto(lot.Id, lot.SupplierBatch, lot.Expiry, lot.Quantity, lot.ReceivedAt,
                    lot.Allocations.Select(a => new AllocationDto(a.RackId, a.LocationId, Path(a.Rack, a.Location), a.Quantity)).ToArray(),
                    lot.Placements.Select(p => new AllocationDto(p.RackId, p.LocationId, paths[(p.RackId, p.LocationId)], p.Quantity)).ToArray(),
                    balances.Where(b => b.StockLotId == lot.Id).Select(ToBalance).ToArray())).ToArray())).ToArray());
    }
    public async Task<PageDto<PlacementDto>> Placements(int page, int pageSize)
    {
        // Direct rack placement is supported even when optional bins/drawers exist beneath it.
        var rackRows = db.StorageRacks.Where(r => r.IsActive && r.Area.IsActive && r.Area.Warehouse.IsActive)
            .Select(r => new { RackId = r.Id, LocationId = (Guid?)null, WarehouseId = r.Area.WarehouseId, Warehouse = r.Area.Warehouse.Code, Area = r.Area.Code, Rack = r.Code, Location = (string?)null });
        var locations = db.StorageLocations.Where(l => l.IsActive && l.Rack.IsActive && l.Rack.Area.IsActive && l.Rack.Area.Warehouse.IsActive)
            .Select(l => new { RackId = l.RackId, LocationId = (Guid?)l.Id, WarehouseId = l.Rack.Area.WarehouseId, Warehouse = l.Rack.Area.Warehouse.Code, Area = l.Rack.Area.Code, Rack = l.Rack.Code, Location = (string?)l.Code });
        var query = rackRows.Concat(locations); var total = await query.CountAsync();
        var items = await query.OrderBy(x => x.Warehouse).ThenBy(x => x.Area).ThenBy(x => x.Rack).ThenBy(x => x.Location).ThenBy(x => x.RackId).ThenBy(x => x.LocationId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync();
        return new(items.Select(x => new PlacementDto(x.RackId, x.LocationId, x.WarehouseId, $"{x.Warehouse} / {x.Area} / {x.Rack}" + (x.Location is null ? " (rack)" : $" / {x.Location}"))).ToArray(), page, pageSize, total);
    }
    public async Task<PageDto<BalanceDto>> Stock(int page, int pageSize, Guid? deliveryNoteId)
    {
        var query = Balances().Where(x => x.Quantity > 0);
        if (deliveryNoteId.HasValue) query = query.Where(x => x.StockLot.DeliveryNoteLine.DeliveryNoteId == deliveryNoteId);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.StockLot.ReceivedAt).ThenBy(x => x.StockLotId).ThenBy(x => x.RackId).ThenBy(x => x.LocationId).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync();
        return new(items.Select(ToBalance).ToArray(), page, pageSize, total);
    }
    public async Task<PageDto<MovementDto>> Movements(Guid id, int page, int pageSize)
    {
        if (!await db.DeliveryNotes.AnyAsync(x => x.Id == id)) throw new ApiProblemException(404, "Delivery note not found.");
        var query = db.StockMovements.AsNoTracking().Where(x => x.DeliveryNoteId == id); var total = await query.CountAsync();
        return new(await query.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new MovementDto(x.Id, x.StockLotId, x.WarehouseId, x.RackId, x.LocationId, x.Type, x.Quantity, x.Timestamp, x.ActorId, x.CorrelationId, x.CorrectionId)).ToArrayAsync(), page, pageSize, total);
    }
    private static BalanceDto ToBalance(InventoryBalance b)
    {
        var lot = b.StockLot; var sellable = lot.Product.IsActive && (!lot.Expiry.HasValue || lot.Expiry >= DateOnly.FromDateTime(DateTime.UtcNow));
        return new(b.Id, lot.DeliveryNoteLine.DeliveryNoteId, lot.DeliveryNoteLine.DeliveryNote.Number, lot.ProductId, lot.Product.Code, lot.Product.Name,
            lot.Id, lot.SupplierBatch, lot.Expiry, lot.ReceivedAt, b.WarehouseId, b.Warehouse.Name, b.RackId, b.LocationId, Path(b.Rack, b.Location), b.Quantity, sellable ? b.Quantity : 0);
    }
    public static string Path(StorageRack rack, StorageLocation? location) => $"{rack.Area.Warehouse.Code} / {rack.Area.Code} / {rack.Code}" + (location is null ? " (rack)" : $" / {location.Code}");
    private async Task<Dictionary<(Guid, Guid?), string>> PlacementPaths(IEnumerable<(Guid RackId, Guid? LocationId)> keys)
    {
        var all = keys.Distinct().ToArray(); var rackIds = all.Select(x => x.RackId).Distinct().ToArray(); var locationIds = all.Where(x => x.LocationId.HasValue).Select(x => x.LocationId!.Value).ToArray();
        var racks = await db.StorageRacks.AsNoTracking().Include(x => x.Area).ThenInclude(x => x.Warehouse).Where(x => rackIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var locations = await db.StorageLocations.AsNoTracking().Where(x => locationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        return all.ToDictionary(key => key, key => Path(racks[key.RackId], key.LocationId.HasValue ? locations[key.LocationId.Value] : null));
    }
}
