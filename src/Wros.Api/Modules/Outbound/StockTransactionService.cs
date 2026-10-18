using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Audit;
using Wros.Api.Modules.Identity;
using Wros.Api.Modules.Inventory;
using Wros.Api.Modules.Purchasing;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Operations;
using Wros.Api.Shared.Persistence;
namespace Wros.Api.Modules.Outbound;

public sealed record StockSelection(Guid SourceBalanceId, decimal Quantity, Guid? DestinationRackId, Guid? DestinationLocationId);
public sealed record StockTransactionRequest(Guid IdempotencyKey, Guid? SupplierId, Guid? DeliveryNoteId, Guid? PurchaseOrderId,
    [MaxLength(2000)] string? Remarks, bool PhysicalCompletionConfirmed, [Required, MinLength(1), MaxLength(500)] StockSelection[] Lines);
public sealed class StockTransactionService(WrosDbContext db, AuditService audit, DocumentNumbers numbers)
{
    public static void Kind(string kind)
    {
        if (kind is not ("SupplierReturn" or "Disposal" or "Transfer"))
            throw new ApiProblemException(400, "Choose a valid transaction type.");
    }
    public async Task<StockTransaction> Post(string kind, StockTransactionRequest r)
    {
        Kind(kind);
        if (!r.PhysicalCompletionConfirmed || r.IdempotencyKey == Guid.Empty || r.Lines is null || r.Lines.Length is < 1 or > 500 || r.Lines.Any(x => x is null))
            throw new ApiProblemException(400, "Confirm physical completion, provide a stable request key and 1–500 valid selections.");
        foreach (var l in r.Lines)
            PurchaseOrderService.Quantity(l.Quantity, false);
        var actor = audit.ActorId ?? throw new ApiProblemException(401, "Staff identity required.");
        var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            kind,
            actor,
            Request = r
        })));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var prior = await db.Set<StockTransaction>().Include(x => x.Lines).AsNoTracking().SingleOrDefaultAsync(x => x.IdempotencyKey == r.IdempotencyKey);
        if (prior != null)
        {
            if (prior.RequestHash != hash)
                throw new ApiProblemException(409, "This request key was already used for different transaction data.");
            return prior;
        }
        if (kind == "SupplierReturn" && (r.SupplierId == null || !await db.Suppliers.AnyAsync(s => s.Id == r.SupplierId && s.IsActive)))
            throw new ApiProblemException(400, "Choose an active supplier.");
        if (kind != "SupplierReturn" && r.SupplierId.HasValue)
            throw new ApiProblemException(400, "Supplier is only applicable to a supplier return.");
        if (kind == "Disposal" && string.IsNullOrWhiteSpace(r.Remarks))
            throw new ApiProblemException(400, "Enter a short disposal reason.");
        if (r.DeliveryNoteId.HasValue && !await db.DeliveryNotes.AnyAsync(n => n.Id == r.DeliveryNoteId && n.Status == "Posted"))
            throw new ApiProblemException(400, "Original receipt must exist and be posted.");
        if (r.PurchaseOrderId.HasValue && !await db.PurchaseOrders.AnyAsync(p => p.Id == r.PurchaseOrderId))
            throw new ApiProblemException(400, "Purchase order reference not found.");
        var record = new StockTransaction { Kind = kind, IdempotencyKey = r.IdempotencyKey, RequestHash = hash, SupplierId = r.SupplierId, DeliveryNoteId = r.DeliveryNoteId, PurchaseOrderId = r.PurchaseOrderId, Remarks = OperationRules.Optional(r.Remarks, 2000, "Remarks"), ActorId = actor, CorrelationId = audit.CorrelationId };
        record.Number = await numbers.Next(kind switch
        {
            "SupplierReturn" => "SR",
            "Disposal" => "DS",
            _ => "WT"
        });
        var ids = r.Lines.Select(x => x.SourceBalanceId).Distinct().Order().ToArray();
        var balances = await db.InventoryBalances.Include(b => b.StockLot).ThenInclude(l => l.Product).Include(b => b.StockLot).ThenInclude(l => l.DeliveryNoteLine)
            .Include(b => b.Rack).ThenInclude(x => x.Area).ThenInclude(x => x.Warehouse).Include(b => b.Location)
            .Where(b => ids.Contains(b.Id)).OrderBy(b => b.Id).ToDictionaryAsync(b => b.Id);
        foreach (var group in r.Lines.GroupBy(x => x.SourceBalanceId))
        {
            if (!balances.TryGetValue(group.Key, out var b) || b.Quantity < group.Sum(x => x.Quantity))
                throw new ApiProblemException(409, "Insufficient source stock. Refresh balances and reduce quantities.");
            ValidateSource(b);
        }
        var destinationRackIds = r.Lines.Where(l => l.DestinationRackId.HasValue).Select(l => l.DestinationRackId!.Value).Distinct().ToArray();
        var destinationLocationIds = r.Lines.Where(l => l.DestinationLocationId.HasValue).Select(l => l.DestinationLocationId!.Value).Distinct().ToArray();
        var racks = await db.StorageRacks.Include(x => x.Area).ThenInclude(x => x.Warehouse).Where(x => destinationRackIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var locations = await db.StorageLocations.Where(x => destinationLocationIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id);
        var lotIds = balances.Values.Select(b => b.StockLotId).Distinct().ToArray();
        var destinationBalances = await db.InventoryBalances.Where(b => lotIds.Contains(b.StockLotId) && destinationRackIds.Contains(b.RackId)).ToDictionaryAsync(b => (b.StockLotId, b.RackId, b.LocationId));
        foreach (var l in r.Lines)
        {
            var b = balances[l.SourceBalanceId];
            b.Quantity -= l.Quantity;
            b.UpdatedAt = DateTimeOffset.UtcNow;
            b.UpdatedBy = actor;
            var line = new StockTransactionLine { SourceBalanceId = b.Id, Quantity = l.Quantity, DestinationRackId = l.DestinationRackId, DestinationLocationId = l.DestinationLocationId };
            record.Lines.Add(line);
            AddMovement(b, kind == "Transfer" ? "TransferOut" : kind, -l.Quantity, record);
            if (kind != "Transfer")
            {
                if (l.DestinationRackId.HasValue || l.DestinationLocationId.HasValue)
                    throw new ApiProblemException(400, "Destination is only applicable to transfers.");
                continue;
            }
            if (!l.DestinationRackId.HasValue)
                throw new ApiProblemException(400, "Choose a destination rack for every transfer selection.");
            racks.TryGetValue(l.DestinationRackId.Value, out var rack);
            if (rack == null || !rack.IsActive || !rack.Area.IsActive || !rack.Area.Warehouse.IsActive || rack.Area.WarehouseId == b.WarehouseId)
                throw new ApiProblemException(400, "Choose an active rack in a different warehouse.");
            if (l.DestinationLocationId.HasValue && (!locations.TryGetValue(l.DestinationLocationId.Value, out var location) || location.RackId != rack.Id || !location.IsActive))
                throw new ApiProblemException(400, "Destination position must be active and belong to its rack.");
            var key = (b.StockLotId, rack.Id, l.DestinationLocationId);
            if (!destinationBalances.TryGetValue(key, out var dest))
            {
                dest = new InventoryBalance { StockLotId = b.StockLotId, StockLot = b.StockLot, WarehouseId = rack.Area.WarehouseId, RackId = rack.Id, LocationId = l.DestinationLocationId, CreatedBy = actor };
                db.InventoryBalances.Add(dest);
                destinationBalances[key] = dest;
            }
            dest.Quantity += l.Quantity;
            dest.UpdatedAt = DateTimeOffset.UtcNow;
            dest.UpdatedBy = actor;
            AddMovement(dest, "TransferIn", l.Quantity, record);
        }
        db.Set<StockTransaction>().Add(record);
        audit.Record(kind + "Posted", "StockTransaction", record.Id.ToString(), null, new
        {
            record.Number,
            record.Kind,
            record.SupplierId,
            record.DeliveryNoteId,
            record.PurchaseOrderId,
            Lines = record.Lines.Select(x => new { x.SourceBalanceId, x.Quantity, x.DestinationRackId, x.DestinationLocationId })
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return record;
    }
    public static void ValidateSource(InventoryBalance b)
    {
        if (!b.StockLot.Product.IsActive || !b.Rack.IsActive || !b.Rack.Area.IsActive || !b.Rack.Area.Warehouse.IsActive || (b.LocationId.HasValue && b.Location?.IsActive != true))
            throw new ApiProblemException(400, "Source product and the complete location hierarchy must be active.");
    }
    private void AddMovement(InventoryBalance b, string type, decimal quantity, StockTransaction record) => db.StockMovements.Add(new StockMovement { DeliveryNoteId = b.StockLot.DeliveryNoteLine.DeliveryNoteId, StockLotId = b.StockLotId, WarehouseId = b.WarehouseId, RackId = b.RackId, LocationId = b.LocationId, Type = type, Quantity = quantity, ActorId = audit.ActorId, CorrelationId = audit.CorrelationId, ReferenceId = record.Id, ReferenceType = record.Kind, ReferenceNumber = record.Number });
    public async Task<PageDto<object>> List(string kind, int page, int pageSize)
    {
        Kind(kind);
        var q = db.Set<StockTransaction>().AsNoTracking().Where(t => t.Kind == kind);
        return new(await q.OrderByDescending(t => t.CompletedAt).ThenByDescending(t => t.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(t => (object)new { t.Id, t.Number, t.Kind, t.CompletedAt, t.ActorId, t.CorrelationId, t.Remarks, Quantity = t.Lines.Sum(l => l.Quantity) }).ToArrayAsync(), page, pageSize, await q.CountAsync());
    }
    public async Task<object> Get(string kind, Guid id)
    {
        Kind(kind);
        var t = await db.Set<StockTransaction>().AsNoTracking().Include(x => x.Supplier).Include(x => x.Lines).ThenInclude(x => x.SourceBalance).ThenInclude(x => x.StockLot).ThenInclude(x => x.Product)
            .Include(x => x.Lines).ThenInclude(x => x.SourceBalance).ThenInclude(x => x.Rack).ThenInclude(x => x.Area).ThenInclude(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.SourceBalance).ThenInclude(x => x.Location)
            .Include(x => x.Lines).ThenInclude(x => x.DestinationRack).ThenInclude(x => x!.Area).ThenInclude(x => x.Warehouse)
            .Include(x => x.Lines).ThenInclude(x => x.DestinationLocation).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id && x.Kind == kind) ?? throw new ApiProblemException(404, "Transaction not found.");
        return new
        {
            t.Id,
            t.Number,
            t.Kind,
            t.CompletedAt,
            t.ActorId,
            t.CorrelationId,
            t.Remarks,
            t.SupplierId,
            Supplier = t.Supplier?.Name,
            t.DeliveryNoteId,
            t.PurchaseOrderId,
            Lines = t.Lines.Select(l => new { l.Id, l.SourceBalanceId, l.SourceBalance.StockLotId, l.SourceBalance.WarehouseId, l.SourceBalance.RackId, l.SourceBalance.LocationId, Sku = l.SourceBalance.StockLot.Product.Code, SourcePath = Wros.Api.Modules.Receiving.ReceiptQueries.Path(l.SourceBalance.Rack, l.SourceBalance.Location), DestinationPath = l.DestinationRack == null ? null : Wros.Api.Modules.Receiving.ReceiptQueries.Path(l.DestinationRack, l.DestinationLocation), l.Quantity, l.DestinationRackId, l.DestinationLocationId })
        };
    }
}
