using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wros.Api.Modules.Audit;
using Wros.Api.Modules.Inventory;
using Wros.Api.Modules.Outbound;
using Wros.Api.Modules.Purchasing;
using Wros.Api.Shared.Configuration;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Operations;
using Wros.Api.Shared.Persistence;
namespace Wros.Api.Modules.Integration;

public sealed record ClientRequest([Required, MaxLength(80)] string Id, [Required, MaxLength(200)] string Name, bool IsActive, string? RowVersion);
public sealed record MappingRequest([Required, MaxLength(80)] string ClientId, [Required, MaxLength(100)] string ExternalProductId, Guid ProductId, bool IsActive, string? RowVersion);
public sealed record ExternalSelection(Guid BalanceId, Guid StockLotId, Guid WarehouseId, Guid RackId, Guid? LocationId, decimal Quantity);
public sealed record ExternalLine([Required, MaxLength(100)] string ExternalLineId, [Required, MaxLength(100)] string ExternalProductId, Guid ProductId, decimal Quantity, [Required, MinLength(1), MaxLength(500)] ExternalSelection[] Selections);
public sealed record DeductionRequest([Required, MaxLength(100)] string EventId, [Required, MaxLength(100)] string ExternalOrderId, [Required, MinLength(1), MaxLength(500)] ExternalLine[] Lines);
public sealed class IntegrationService(WrosDbContext db, InventoryQueries inventory, AuditService audit, IOptions<IntegrationOptions> options)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<object> Status(int page, int pageSize)
    {
        var config = options.Value;
        return new
        {
            Audience = config.Audience,
            config.SnapshotMinutes,
            config.MaxSnapshotRows,
            Clients = await db.Set<IntegrationClient>().AsNoTracking().OrderBy(c => c.Id).Select(c => new { c.Id, c.Name, c.IsActive, RowVersion = Convert.ToBase64String(c.RowVersion) }).ToArrayAsync(),
            Configured = config.AllowedClients.Select(id => new { Id = id, Scopes = config.Clients.Single(c => c.Id == id).Scopes, KeyIds = config.Clients.Single(c => c.Id == id).Keys.Select(k => k.KeyId) }),
            RecentDeductions = await db.Set<ExternalOrderDeduction>().AsNoTracking().OrderByDescending(d => d.CompletedAt).ThenBy(d => d.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(d => new { d.Id, d.ClientId, d.EventId, d.ExternalOrderId, d.CompletedAt, d.CorrelationId, Quantity = d.Lines.Sum(l => l.Quantity) }).ToArrayAsync(),
            TotalDeductions = await db.Set<ExternalOrderDeduction>().CountAsync(),
            RecentSnapshots = await db.Set<InventorySyncSnapshot>().AsNoTracking().OrderByDescending(s => s.Version).Take(20).Select(s => new { s.Id, s.ClientId, s.Version, s.CreatedAt, s.ExpiresAt, s.TotalRows }).ToArrayAsync()
        };
    }
    public async Task<object> SaveClient(ClientRequest r)
    {
        var id = OperationRules.Required(r.Id, 80, "Client ID");
        if (!System.Text.RegularExpressions.Regex.IsMatch(id, "^[A-Za-z0-9._-]+$"))
            throw new ApiProblemException(400, "Client ID uses letters, digits, dots, underscores or hyphens.");
        var c = await db.Set<IntegrationClient>().SingleOrDefaultAsync(c => c.Id == id);
        object? before = c == null ? null : new
        {
            c.Name,
            c.IsActive
        };
        if (c == null)
        {
            c = new IntegrationClient { Id = id };
            db.Add(c);
        }
        else
            CheckVersion(c.RowVersion, r.RowVersion);
        c.Name = OperationRules.Required(r.Name, 200, "Client name");
        c.IsActive = r.IsActive;
        audit.Record("IntegrationClientChanged", "IntegrationClient", id, before, new
        {
            c.Name,
            c.IsActive
        });
        await db.SaveChangesAsync();
        return new
        {
            c.Id,
            c.Name,
            c.IsActive,
            RowVersion = Convert.ToBase64String(c.RowVersion)
        };
    }
    public async Task<object> Mappings(string? clientId, int page, int pageSize)
    {
        var q = db.Set<ProductMapping>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(clientId))
            q = q.Where(x => x.ClientId == clientId);
        return new
        {
            Items = await q.OrderBy(x => x.ClientId).ThenBy(x => x.ExternalProductId).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { x.Id, x.ClientId, x.ExternalProductId, x.ProductId, Sku = x.Product.Code, Product = x.Product.Name, x.IsActive, RowVersion = Convert.ToBase64String(x.RowVersion) }).ToArrayAsync(),
            Page = page,
            PageSize = pageSize,
            Total = await q.CountAsync()
        };
    }
    public async Task<object> SaveMapping(Guid? id, MappingRequest r)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        if (!await db.Set<IntegrationClient>().AnyAsync(c => c.Id == r.ClientId) || !await db.Products.AnyAsync(p => p.Id == r.ProductId))
            throw new ApiProblemException(400, "Choose an existing client and product.");
        var external = OperationRules.Required(r.ExternalProductId, 100, "External product ID");
        var m = id.HasValue ? await db.Set<ProductMapping>().SingleOrDefaultAsync(m => m.Id == id) : null;
        if (id.HasValue && m == null)
            throw new ApiProblemException(404, "Mapping not found.");
        object? before = m == null ? null : new
        {
            m.ClientId,
            m.ExternalProductId,
            m.ProductId,
            m.IsActive
        };
        if (m == null)
        {
            m = new ProductMapping();
            db.Add(m);
        }
        else
            CheckVersion(m.RowVersion, r.RowVersion);
        if (await db.Set<ProductMapping>().AnyAsync(x => x.ClientId == r.ClientId && x.ExternalProductId == external && x.Id != m.Id))
            throw new ApiProblemException(409, "This external identifier is already mapped for this client.");
        m.ClientId = r.ClientId;
        m.ExternalProductId = external;
        m.ProductId = r.ProductId;
        m.IsActive = r.IsActive;
        m.UpdatedAt = DateTimeOffset.UtcNow;
        audit.Record("ProductMappingChanged", "ProductMapping", m.Id.ToString(), before, new
        {
            m.ClientId,
            m.ExternalProductId,
            m.ProductId,
            m.IsActive
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new
        {
            m.Id,
            RowVersion = Convert.ToBase64String(m.RowVersion)
        };
    }
    private static void CheckVersion(byte[] actual, string? supplied)
    {
        if (supplied != Convert.ToBase64String(actual))
            throw new ApiProblemException(409, "This record changed. Refresh and retry.");
    }
    public async Task<object> CreateSnapshot(string clientId)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        // Capture serialization also makes the identity version order equal capture/commit order.
        await db.Database.ExecuteSqlRawAsync("DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=N'Wros.InventorySnapshot', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000; IF @result<0 THROW 51000, 'Snapshot capture is busy.',1;");
        var config = options.Value;
        var created = DateTimeOffset.UtcNow;
        var q = inventory.Filter(new InventoryFilter());
        var count = await q.CountAsync();
        if (count > config.MaxSnapshotRows)
            throw new ApiProblemException(413, "Snapshot capacity exceeded. Increase the configured row limit after reviewing database capacity.");
        // Serializable locks cover balances, products and mapping reads until the immutable rows commit.
        var rows = await inventory.Rows(q.OrderBy(b => b.StockLot.ProductId).ThenBy(b => b.StockLotId).ThenBy(b => b.Id)).ToArrayAsync();
        var mappings = await db.Set<ProductMapping>().AsNoTracking().Where(m => m.ClientId == clientId && m.IsActive).Select(m => new { m.ProductId, m.ExternalProductId }).ToArrayAsync();
        var mappingLookup = mappings.ToLookup(m => m.ProductId, m => m.ExternalProductId);
        var s = new InventorySyncSnapshot { ClientId = clientId, CreatedAt = created, ExpiresAt = created.AddMinutes(config.SnapshotMinutes), TotalRows = rows.Length };
        var fifo = new Dictionary<Guid, int>();
        var fefo = new Dictionary<Guid, int>();
        foreach (var product in rows.Where(r => r.SellableQuantity > 0).GroupBy(r => r.ProductId))
        {
            var lots = product.DistinctBy(r => r.StockLotId).ToArray();
            var rank = 0;
            foreach (var lot in lots.OrderBy(r => r.ReceivedAt).ThenBy(r => r.StockLotId))
                fifo[lot.StockLotId] = ++rank;
            rank = 0;
            foreach (var lot in lots.OrderBy(r => r.Expiry == null).ThenBy(r => r.Expiry).ThenBy(r => r.ReceivedAt).ThenBy(r => r.StockLotId))
                fefo[lot.StockLotId] = ++rank;
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var ordinal = 0;
        foreach (var row in rows)
        {
            var payload = JsonSerializer.Serialize(new
            {
                Stock = row,
                ExternalProductIds = mappingLookup[row.ProductId].Order().ToArray(),
                FifoRank = fifo.TryGetValue(row.StockLotId, out var f) ? (int?)f : null,
                FefoRank = fefo.TryGetValue(row.StockLotId, out var e) ? (int?)e : null
            }, Json);
            s.Lines.Add(new InventorySyncSnapshotLine { SnapshotId = s.Id, Ordinal = ++ordinal, Payload = payload });
            hash.AppendData(Encoding.UTF8.GetBytes(payload + "\n"));
        }
        s.ContentHash = Convert.ToHexString(hash.GetHashAndReset());
        db.Add(s);
        audit.Record("InventorySnapshotCreated", "InventorySnapshot", s.Id.ToString(), null, new
        {
            ClientId = clientId,
            s.TotalRows,
            s.ExpiresAt
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return SnapshotHeader(s);
    }
    private static object SnapshotHeader(InventorySyncSnapshot s) => new { s.Id, Version = s.Version.ToString(System.Globalization.CultureInfo.InvariantCulture), s.CreatedAt, s.ExpiresAt, s.TotalRows, s.ContentHash, HashAlgorithm = "SHA256 of UTF-8 payload strings plus LF in ordinal order", PageSizeLimit = 100 };
    public async Task<object> Snapshot(string clientId, Guid id, int page, int pageSize)
    {
        var s = await db.Set<InventorySyncSnapshot>().AsNoTracking().SingleOrDefaultAsync(s => s.Id == id && s.ClientId == clientId) ?? throw new ApiProblemException(404, "Snapshot not found for this client.");
        if (s.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new ApiProblemException(410, "Snapshot expired. Discard staged pages and create a new snapshot.");
        var rows = await db.Set<InventorySyncSnapshotLine>().AsNoTracking().Where(l => l.SnapshotId == id).OrderBy(l => l.Ordinal).Skip((page - 1) * pageSize).Take(pageSize).Select(l => new { l.Ordinal, l.Payload }).ToArrayAsync();
        audit.Record("InventorySnapshotPageRead", "InventorySnapshot", id.ToString(), null, new
        {
            ClientId = clientId,
            Page = page,
            Count = rows.Length
        });
        await db.SaveChangesAsync();
        return new
        {
            Snapshot = SnapshotHeader(s),
            Page = page,
            PageSize = pageSize,
            Items = rows.Select(l => new { l.Ordinal, l.Payload, Data = JsonSerializer.Deserialize<JsonElement>(l.Payload) }),
            Complete = rows.Length > 0 && rows[^1].Ordinal == s.TotalRows || s.TotalRows == 0
        };
    }
    public async Task<object> Deduct(string clientId, DeductionRequest r)
    {
        if (r.Lines == null || r.Lines.Length is < 1 or > 500 || r.Lines.Any(l => l == null || l.Selections == null || l.Selections.Length == 0 || l.Selections.Any(s => s == null)) || r.Lines.Sum(l => l.Selections.Length) > 5000)
            throw new ApiProblemException(400, "Provide valid order lines and at most 5,000 selections.");
        var eventId = OperationRules.Required(r.EventId, 100, "Event ID");
        var orderId = OperationRules.Required(r.ExternalOrderId, 100, "Order ID");
        if (r.Lines.Select(l => l.ExternalLineId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != r.Lines.Length)
            throw new ApiProblemException(400, "External order line IDs must be distinct.");
        foreach (var l in r.Lines)
        {
            OperationRules.Required(l.ExternalLineId, 100, "Line ID");
            OperationRules.Required(l.ExternalProductId, 100, "External product ID");
            PurchaseOrderService.Quantity(l.Quantity, false);
            foreach (var s in l.Selections)
                PurchaseOrderService.Quantity(s.Quantity, false);
            if (l.Selections.Sum(s => s.Quantity) != l.Quantity)
                throw new ApiProblemException(400, "Each line's selected quantities must equal its ordered quantity.");
        }
        var requestHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(r, Json)));
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var prior = await db.Set<ExternalOrderDeduction>().AsNoTracking().SingleOrDefaultAsync(d => d.ClientId == clientId && d.EventId == eventId);
        if (prior != null)
        {
            if (prior.RequestHash != requestHash)
                throw new ApiProblemException(409, "Conflicting reuse of an integration event key.");
            return DeductionResult(prior);
        }
        if (await db.Set<ExternalOrderDeduction>().AnyAsync(d => d.ClientId == clientId && d.ExternalOrderId == orderId))
            throw new ApiProblemException(409, "This external order was already deducted under another event key.");
        var externalIds = r.Lines.Select(l => l.ExternalProductId).Distinct().ToArray();
        var mappings = await db.Set<ProductMapping>().Where(m => m.ClientId == clientId && m.IsActive && externalIds.Contains(m.ExternalProductId)).ToArrayAsync();
        foreach (var l in r.Lines)
        if (!mappings.Any(m => m.ExternalProductId == l.ExternalProductId && m.ProductId == l.ProductId))
            throw new ApiProblemException(422, "Missing or mismatched active product mapping.");
        var selections = r.Lines.SelectMany(l => l.Selections).ToArray();
        var ids = selections.Select(s => s.BalanceId).Distinct().Order().ToArray();
        var balances = await db.InventoryBalances.Include(b => b.StockLot).ThenInclude(l => l.Product).Include(b => b.StockLot).ThenInclude(l => l.DeliveryNoteLine).Include(b => b.Rack).ThenInclude(x => x.Area).ThenInclude(x => x.Warehouse).Include(b => b.Location).Where(b => ids.Contains(b.Id)).OrderBy(b => b.Id).ToDictionaryAsync(b => b.Id);
        foreach (var group in selections.GroupBy(s => s.BalanceId))
        if (!balances.TryGetValue(group.Key, out var b) || b.Quantity < group.Sum(s => s.Quantity))
            throw new ApiProblemException(409, "Insufficient available stock; no allocations were deducted.");
        var deduction = new ExternalOrderDeduction { ClientId = clientId, EventId = eventId, ExternalOrderId = orderId, RequestHash = requestHash, CorrelationId = audit.CorrelationId };
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var line in r.Lines)
        foreach (var s in line.Selections)
        {
            var b = balances[s.BalanceId];
            StockTransactionService.ValidateSource(b);
            if (b.StockLot.ProductId != line.ProductId || b.StockLotId != s.StockLotId || b.WarehouseId != s.WarehouseId || b.RackId != s.RackId || b.LocationId != s.LocationId)
                throw new ApiProblemException(422, "Selected product, lot and placement identifiers do not match.");
            if (b.StockLot.Expiry < today)
                throw new ApiProblemException(422, "Expired stock is not sellable.");
            b.Quantity -= s.Quantity;
            b.UpdatedAt = DateTimeOffset.UtcNow;
            b.UpdatedBy = null; // Machine identity is recorded explicitly on the immutable movement.
            deduction.Lines.Add(new ExternalOrderDeductionLine { ExternalLineId = line.ExternalLineId, ExternalProductId = line.ExternalProductId, ProductId = line.ProductId, BalanceId = b.Id, Quantity = s.Quantity });
            db.StockMovements.Add(new StockMovement { DeliveryNoteId = b.StockLot.DeliveryNoteLine.DeliveryNoteId, StockLotId = b.StockLotId, WarehouseId = b.WarehouseId, RackId = b.RackId, LocationId = b.LocationId, Type = "ExternalOrder", Quantity = -s.Quantity, ReferenceId = deduction.Id, ReferenceType = "ExternalOrder", ReferenceNumber = "RTD-" + deduction.Id.ToString("N"), CorrelationId = audit.CorrelationId, IntegrationClientId = clientId });
        }
        db.Add(deduction);
        audit.Record("ReadyToDeliverDeducted", "ExternalOrderDeduction", deduction.Id.ToString(), null, new
        {
            ClientId = clientId,
            EventId = eventId,
            ExternalOrderId = orderId,
            Lines = deduction.Lines.Select(l => new { l.ExternalLineId, l.ExternalProductId, l.ProductId, l.BalanceId, l.Quantity })
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return DeductionResult(deduction);
    }
    private static object DeductionResult(ExternalOrderDeduction d) => new { d.Id, d.ClientId, d.EventId, d.ExternalOrderId, d.CompletedAt, d.CorrelationId, Status = "Deducted" };
}
