using System.Data;
using Wros.Api.Modules.Purchasing.SupplierCatalog;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Audit;
using Wros.Api.Modules.Identity;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Operations;
using Wros.Api.Shared.Persistence;
namespace Wros.Api.Modules.Purchasing;
public sealed class PurchaseOrderService(WrosDbContext db, AuditService audit, DocumentNumbers numbers)
{
    public async Task<PageDto<OrderDto>> List(int page, int pageSize, string? search, string? status)
    {
        var query = Query().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = OperationRules.Required(search, 100, "Search"); query = query.Where(x => x.Number.Contains(term) || x.Supplier.Name.Contains(term)); }
        if (!string.IsNullOrEmpty(status))
        {
            if (status is not ("Draft" or "Sent" or "Locked")) throw new ApiProblemException(400, "Invalid purchase order status.");
            query = query.Where(x => x.Status == status);
        }
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return new(items.Select(ToDto).ToArray(), page, pageSize, total);
    }
    public async Task<OrderDto> Get(Guid id) => ToDto(await Query().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiProblemException(404, "Purchase order not found."));
    public async Task<RevisionDto[]> History(Guid id)
    {
        if (!await db.PurchaseOrders.AnyAsync(x => x.Id == id)) throw new ApiProblemException(404, "Purchase order not found.");
        return await db.PurchaseOrderRevisions.AsNoTracking().Where(x => x.PurchaseOrderId == id).OrderByDescending(x => x.Revision)
            .Select(x => new RevisionDto(x.Revision, x.Action, x.Timestamp, x.ActorId, x.Snapshot)).ToArrayAsync();
    }
    public async Task<OrderDto> Save(Guid? id, OrderRequest request)
    {
        if (request.OrderDate == default) throw new ApiProblemException(400, "PO date is required.");
        if (request.Lines is null || request.Lines.Length is < 1 or > 500 || request.Lines.Any(x => x is null)) throw new ApiProblemException(400, "Choose 1–500 product lines.");
        foreach (var line in request.Lines) Quantity(line.Quantity, false);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = id.HasValue ? await Query().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiProblemException(404, "Purchase order not found.") : new PurchaseOrder();
        if (id.HasValue) { OperationRules.CheckVersion(order, request.RowVersion); EnsureEditable(order); }
        var supplier = await db.Suppliers.SingleOrDefaultAsync(x => x.Id == request.SupplierId && x.IsActive) ?? throw new ApiProblemException(400, "Choose an active supplier.");
        var productIds = request.Lines.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products.Where(x => productIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id);
        if (products.Count != productIds.Length) throw new ApiProblemException(400, "Choose active products for all lines.");
        var original = order.Lines.ToDictionary(x => x.Id);
        var catalogIds = request.Lines.Where(x => x.SupplierProductId.HasValue).Select(x => x.SupplierProductId!.Value).Distinct().ToArray();
        if (request.Lines.Where(x => x.SupplierProductId.HasValue).GroupBy(x => x.SupplierProductId).Any(x => x.Count() > 1)) throw new ApiProblemException(400, "A supplier product may appear only once in a PO.");
        if (request.Lines.Where(x => x.OriginalLineId.HasValue).GroupBy(x => x.OriginalLineId).Any(x => x.Count() > 1)) throw new ApiProblemException(400, "Original PO lines cannot be duplicated.");
        var catalog = await db.SupplierProducts.Where(x => catalogIds.Contains(x.Id) && x.SupplierId == supplier.Id).ToDictionaryAsync(x => x.Id);
        var prepared = request.Lines.Select((line, position) =>
        {
            var next = new PurchaseOrderLine { PurchaseOrderId = order.Id, ProductId = line.ProductId, Product = products[line.ProductId], Quantity = line.Quantity, Position = position, SupplierProductId = line.SupplierProductId };
            PurchaseOrderLine? old = null;
            if (line.OriginalLineId.HasValue && !original.TryGetValue(line.OriginalLineId.Value, out old)) throw new ApiProblemException(400, "Original line does not belong to this purchase order.");
            if (line.SupplierProductId.HasValue)
            {
                if (!catalog.TryGetValue(line.SupplierProductId.Value, out var item)) throw new ApiProblemException(400, "Choose an active supplier product explicitly mapped to the selected WROS product.");
                if (!line.RefreshPrice && old?.PricingJson is not null && old.SupplierProductId == item.Id && old.ProductId == line.ProductId && order.SupplierId == supplier.Id)
                {
                    var saved = JsonSerializer.Deserialize<PricingSnapshot>(old.PricingJson)!;
                    next.PricingJson = JsonSerializer.Serialize(saved with { Quantity = line.Quantity, GrossAmount = PurchasePricing.Money(saved.SupplierPrice * line.Quantity), DiscountAmount = PurchasePricing.Money((saved.SupplierPrice - saved.NetUnitPrice) * line.Quantity), LineTotal = PurchasePricing.Money(saved.NetUnitPrice * line.Quantity) });
                }
                else
                {
                    if (item.ProductId != line.ProductId || !item.IsActive) throw new ApiProblemException(400, "Choose an active supplier product explicitly mapped to the selected WROS product.");
                    OperationRules.CheckVersion(item, line.CatalogRowVersion);
                    next.PricingJson = JsonSerializer.Serialize(PurchasePricing.Snapshot(item, line.Quantity));
                }
            }
            else if (old is not null && (old.SupplierProductId.HasValue || old.ProductId != line.ProductId)) throw new ApiProblemException(400, "An existing supplier pricing snapshot cannot be removed or reassigned.");
            else if (old is null) throw new ApiProblemException(400, "New PO lines require a mapped supplier catalog product and price.");
            return next;
        }).ToList();
        if (id.HasValue)
        {
            db.PurchaseOrderLines.RemoveRange(order.Lines.ToArray()); order.Lines.Clear();
            // Flush removed positional keys before replacing the line collection; transaction remains open.
            await db.SaveChangesAsync();
        }
        else { order.Number = await numbers.Next("PO"); order.CreatedBy = audit.ActorId; db.PurchaseOrders.Add(order); }
        order.Supplier = supplier; order.SupplierId = supplier.Id; order.OrderDate = request.OrderDate;
        order.ExpectedDeliveryDate = request.ExpectedDeliveryDate; order.Remarks = OperationRules.Optional(request.Remarks, 2000, "Remarks");
        order.UpdatedAt = DateTimeOffset.UtcNow; order.UpdatedBy = audit.ActorId;
        order.Lines = prepared;
        db.PurchaseOrderLines.AddRange(order.Lines);
        Snapshot(order, id.HasValue ? "Edited" : "Created");
        await db.SaveChangesAsync(); await tx.CommitAsync(); return ToDto(order);
    }
    public async Task<OrderDto> Send(Guid id, VersionRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await Query().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiProblemException(404, "Purchase order not found.");
        OperationRules.CheckVersion(order, request.RowVersion); EnsureEditable(order);
        if (!order.Supplier.IsActive || order.Lines.Any(x => !x.Product.IsActive)) throw new ApiProblemException(400, "Supplier and products must be active before recording a send.");
        var action = order.Status == "Draft" ? "Sent" : "Resent";
        order.Status = "Sent"; order.UpdatedAt = DateTimeOffset.UtcNow; order.UpdatedBy = audit.ActorId;
        Snapshot(order, action); await db.SaveChangesAsync(); await tx.CommitAsync(); return ToDto(order);
    }
    public void LockForReceiving(PurchaseOrder order)
    {
        if (order.Status == "Locked") return;
        order.Status = "Locked"; order.UpdatedAt = DateTimeOffset.UtcNow; order.UpdatedBy = audit.ActorId;
        Snapshot(order, "ReceivingStarted");
    }
    public async Task<OrderDto> PreparePrint(Guid id, VersionRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var order = await Query().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiProblemException(404, "Purchase order not found.");
        OperationRules.CheckVersion(order, request.RowVersion);
        audit.Record("PurchaseOrderPrintPrepared", "PurchaseOrder", id.ToString(), newValues: new { order.Number, order.Revision });
        await db.SaveChangesAsync(); await tx.CommitAsync(); return ToDto(order);
    }
    private void Snapshot(PurchaseOrder order, string action)
    {
        order.Revision += 1;
        // Safe business document snapshot, with product identifiers/labels at this revision. No supplier contact PII.
        var revision = new PurchaseOrderRevision { PurchaseOrderId = order.Id, Revision = order.Revision, Action = action, ActorId = audit.ActorId,
            Snapshot = JsonSerializer.Serialize(new { order.Number, order.SupplierId, Supplier = order.Supplier.Name, order.OrderDate, order.ExpectedDeliveryDate,
                order.Remarks, order.Status, Lines = order.Lines.OrderBy(x => x.Position).Select(x => new { x.ProductId, Sku = x.Product.Code, Name = x.Product.Name, x.Quantity, Pricing = ReadPricing(x) }) }) };
        db.PurchaseOrderRevisions.Add(revision);
        audit.Record($"PurchaseOrder{action}", "PurchaseOrder", order.Id.ToString(), newValues: new { order.Number, order.Status, order.Revision });
    }
    private static void EnsureEditable(PurchaseOrder order)
    {
        if (order.Status == "Locked") throw new ApiProblemException(409, "Receiving has started. This purchase order is locked.");
    }
    public static void Quantity(decimal value, bool zeroAllowed)
    {
        if (value < (zeroAllowed ? 0 : 1) || value > 9007199254740991m || value != decimal.Truncate(value))
            throw new ApiProblemException(400, zeroAllowed ? "Quantities must be non-negative whole units within the supported integer range." : "Quantities must be positive whole units within the supported integer range.");
    }
    private static PricingSnapshot? ReadPricing(PurchaseOrderLine line) => line.PricingJson is null ? null : JsonSerializer.Deserialize<PricingSnapshot>(line.PricingJson);
    private static PricingSummary Summary(PurchaseOrder order)
    {
        var prices = order.Lines.Select(ReadPricing).OfType<PricingSnapshot>().ToArray();
        return new(order.Lines.Count, order.Lines.Sum(x => x.Quantity), prices.Sum(x => x.GrossAmount), prices.Sum(x => x.DiscountAmount), prices.Sum(x => x.LineTotal), order.Lines.Count - prices.Length);
    }
    private IQueryable<PurchaseOrder> Query() => db.PurchaseOrders.Include(x => x.Supplier).Include(x => x.Lines).ThenInclude(x => x.Product).AsSplitQuery();
    private static OrderDto ToDto(PurchaseOrder x) => new(x.Id, x.Number, x.SupplierId, x.Supplier.Code, x.Supplier.Name,
        x.OrderDate, x.ExpectedDeliveryDate, x.Remarks, x.Status, x.Revision, Convert.ToBase64String(x.RowVersion), x.CreatedAt, x.UpdatedAt, x.CreatedBy, x.UpdatedBy,
        x.Lines.OrderBy(l => l.Position).Select(l => new OrderLineDto(l.Id, l.ProductId, ReadPricing(l)?.SupplierProductCode ?? l.Product.Code, ReadPricing(l)?.ProductName ?? l.Product.Name, l.Quantity, l.SupplierProductId, ReadPricing(l))).ToArray(), Summary(x));
}
