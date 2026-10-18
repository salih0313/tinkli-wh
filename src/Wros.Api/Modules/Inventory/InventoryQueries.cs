using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wros.Api.Shared.Configuration;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Persistence;
using Wros.Api.Modules.Identity;
namespace Wros.Api.Modules.Inventory;

public sealed class InventoryFilter
{
    [Range(1, 100000)] public int Page { get; set; } = 1;
    [Range(1, 100)] public int PageSize { get; set; } = 20;
    [MaxLength(100)]
    public string? Search { get; set; }
    public Guid? ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
    public Guid? AreaId { get; set; }
    public Guid? RackId { get; set; }
    public Guid? LotId { get; set; }
    [MaxLength(100)]
    public string? Batch { get; set; }
    [RegularExpression("^(all|expired|near|sellable)$")] public string Expiry { get; set; } = "all";
    [RegularExpression("^(sku|fifo|fefo|quantity)$")] public string Sort { get; set; } = "sku";
}
public sealed record InventoryRow(Guid Id, Guid ProductId, string Sku, string Product, string? Barcode, string? Brand,
    Guid StockLotId, string? SupplierBatch, DateOnly? Expiry, DateTimeOffset ReceivedAt, Guid DeliveryNoteId, string DeliveryNoteNumber,
    Guid WarehouseId, string Warehouse, Guid AreaId, Guid RackId, Guid? LocationId, string Path, decimal Quantity, decimal SellableQuantity);
public sealed class InventoryQueries(WrosDbContext db, IOptions<InventoryOptions> options)
{
    public IQueryable<InventoryBalance> Filter(InventoryFilter f)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var until = today.AddDays(options.Value.ExpiryAlertDays);
        var q = db.InventoryBalances.AsNoTracking().Where(b => b.Quantity > 0);
        if (f.Search is { Length: > 0 })
            q = q.Where(b => b.StockLot.Product.Code.Contains(f.Search) || b.StockLot.Product.Name.Contains(f.Search) || (b.StockLot.Product.Barcode != null && b.StockLot.Product.Barcode.Contains(f.Search)) || (b.StockLot.Product.Brand != null && b.StockLot.Product.Brand.Contains(f.Search)));
        if (f.ProductId.HasValue)
            q = q.Where(b => b.StockLot.ProductId == f.ProductId);
        if (f.WarehouseId.HasValue)
            q = q.Where(b => b.WarehouseId == f.WarehouseId);
        if (f.AreaId.HasValue)
            q = q.Where(b => b.Rack.AreaId == f.AreaId);
        if (f.RackId.HasValue)
            q = q.Where(b => b.RackId == f.RackId);
        if (f.LotId.HasValue)
            q = q.Where(b => b.StockLotId == f.LotId);
        if (f.Batch is { Length: > 0 })
            q = q.Where(b => b.StockLot.SupplierBatch != null && b.StockLot.SupplierBatch.Contains(f.Batch));
        return f.Expiry switch
        {
            "expired" => q.Where(b => b.StockLot.Expiry < today),
            "near" => q.Where(b => b.StockLot.Expiry >= today && b.StockLot.Expiry <= until),
            "sellable" => q.Where(b => b.StockLot.Product.IsActive && (b.StockLot.Expiry == null || b.StockLot.Expiry >= today)),
            _ => q
        };
    }
    public IQueryable<InventoryRow> Rows(IQueryable<InventoryBalance> q)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return q.Select(b => new InventoryRow(b.Id, b.StockLot.ProductId, b.StockLot.Product.Code, b.StockLot.Product.Name, b.StockLot.Product.Barcode, b.StockLot.Product.Brand,
            b.StockLotId, b.StockLot.SupplierBatch, b.StockLot.Expiry, b.StockLot.ReceivedAt, b.StockLot.DeliveryNoteLine.DeliveryNoteId, b.StockLot.DeliveryNoteLine.DeliveryNote.Number,
            b.WarehouseId, b.Warehouse.Name, b.Rack.AreaId, b.RackId, b.LocationId,
            b.Warehouse.Code + " / " + b.Rack.Area.Code + " / " + b.Rack.Code + (b.Location == null ? " (rack)" : " / " + b.Location.Code),
            b.Quantity, b.StockLot.Product.IsActive && (b.StockLot.Expiry == null || b.StockLot.Expiry >= today) ? b.Quantity : 0));
    }
    public async Task<PageDto<InventoryRow>> List(InventoryFilter f)
    {
        var q = Filter(f);
        var count = await q.CountAsync();
        var ordered = f.Sort switch
        {
            "fifo" => q.OrderBy(b => b.StockLot.ReceivedAt).ThenBy(b => b.StockLotId).ThenBy(b => b.Id),
            "fefo" => q.OrderBy(b => b.StockLot.Expiry == null).ThenBy(b => b.StockLot.Expiry).ThenBy(b => b.StockLot.ReceivedAt).ThenBy(b => b.StockLotId).ThenBy(b => b.Id),
            "quantity" => q.OrderByDescending(b => b.Quantity).ThenBy(b => b.Id),
            _ => q.OrderBy(b => b.StockLot.Product.Code).ThenBy(b => b.StockLotId).ThenBy(b => b.Id)
        };
        return new(await Rows(ordered.Skip((f.Page - 1) * f.PageSize).Take(f.PageSize)).ToArrayAsync(), f.Page, f.PageSize, count);
    }
    public async Task<object> Products(InventoryFilter f)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var q = Filter(f).GroupBy(b => new { b.StockLot.ProductId, b.StockLot.Product.Code, b.StockLot.Product.Name })
            .Select(g => new { g.Key.ProductId, Sku = g.Key.Code, Product = g.Key.Name, Physical = g.Sum(b => b.Quantity), Sellable = g.Sum(b => b.StockLot.Product.IsActive && (b.StockLot.Expiry == null || b.StockLot.Expiry >= today) ? b.Quantity : 0) });
        return new
        {
            Items = await q.OrderBy(x => x.Sku).ThenBy(x => x.ProductId).Skip((f.Page - 1) * f.PageSize).Take(f.PageSize).ToArrayAsync(),
            f.Page,
            f.PageSize,
            Total = await q.CountAsync()
        };
    }
    public async Task<object> Dashboard()
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var until = today.AddDays(options.Value.ExpiryAlertDays);
        var q = db.InventoryBalances.AsNoTracking().Where(b => b.Quantity > 0);
        return new
        {
            Products = await db.Products.CountAsync(),
            Skus = await db.Products.Select(p => p.Code).Distinct().CountAsync(),
            Physical = await q.SumAsync(b => b.Quantity),
            Sellable = await q.Where(b => b.StockLot.Product.IsActive && (b.StockLot.Expiry == null || b.StockLot.Expiry >= today)).SumAsync(b => b.Quantity),
            Expired = await q.Where(b => b.StockLot.Expiry < today).SumAsync(b => b.Quantity),
            ApproachingExpiry = await q.Where(b => b.StockLot.Expiry >= today && b.StockLot.Expiry <= until).Select(b => b.StockLotId).Distinct().CountAsync(),
            options.Value.ExpiryAlertDays,
            LowStock = "No thresholds configured",
            Unit = "whole units",
            Warehouses = await q.GroupBy(b => new { b.WarehouseId, b.Warehouse.Name }).Select(g => new { g.Key.WarehouseId, g.Key.Name, Physical = g.Sum(b => b.Quantity), Sellable = g.Sum(b => b.StockLot.Product.IsActive && (b.StockLot.Expiry == null || b.StockLot.Expiry >= today) ? b.Quantity : 0) }).OrderBy(x => x.Name).ToArrayAsync(),
            RecentMovements = await Ledger(1, 5, null, null, null)
        };
    }
    public async Task<object> Ledger(int page, int pageSize, string? search, string? type, Guid? lotId)
    {
        var q = db.StockMovements.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
            q = q.Where(m => m.StockLot.Product.Code.Contains(search) || m.StockLot.Product.Name.Contains(search) || m.DeliveryNote.Number.Contains(search) || m.CorrelationId.Contains(search) || (m.ReferenceNumber != null && m.ReferenceNumber.Contains(search)) || (m.ReferenceType == "ExternalOrder" && db.Set<Wros.Api.Modules.Integration.ExternalOrderDeduction>().Any(d => d.Id == m.ReferenceId && d.ExternalOrderId.Contains(search))));
        if (!string.IsNullOrEmpty(type))
            q = q.Where(m => m.Type == type);
        if (lotId.HasValue)
            q = q.Where(m => m.StockLotId == lotId);
        return new
        {
            Items = await q.OrderByDescending(m => m.Timestamp).ThenByDescending(m => m.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(m => new
            {
                m.Id,
                m.Type,
                m.StockLotId,
                Sku = m.StockLot.Product.Code,
                Product = m.StockLot.Product.Name,
                m.Quantity,
                m.WarehouseId,
                m.RackId,
                m.LocationId,
                Path = db.StorageRacks.Where(r => r.Id == m.RackId).Select(r => r.Area.Warehouse.Code + " / " + r.Area.Code + " / " + r.Code).FirstOrDefault() + (m.LocationId == null ? " (rack)" : " / " + db.StorageLocations.Where(l => l.Id == m.LocationId).Select(l => l.Code).FirstOrDefault()),
                m.DeliveryNoteId,
                Reference = m.DeliveryNote.Number,
                m.Timestamp,
                m.ActorId,
                Actor = db.Users.Where(u => u.Id == m.ActorId).Select(u => u.UserName).FirstOrDefault(),
                m.CorrelationId,
                m.CorrectionId,
                m.ReferenceId,
                m.ReferenceType,
                m.ReferenceNumber,
                m.IntegrationClientId,
                ExternalOrderId = db.Set<Wros.Api.Modules.Integration.ExternalOrderDeduction>().Where(d => d.Id == m.ReferenceId && m.ReferenceType == "ExternalOrder").Select(d => d.ExternalOrderId).FirstOrDefault()
            }).ToArrayAsync(),
            Page = page,
            PageSize = pageSize,
            Total = await q.CountAsync()
        };
    }
}
