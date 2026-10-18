using System.Data;
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
        order.Lines = request.Lines.Select((line, position) => new PurchaseOrderLine { PurchaseOrderId = order.Id, ProductId = line.ProductId, Product = products[line.ProductId], Quantity = line.Quantity, Position = position }).ToList();
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
    public async Task PreparePrint(Guid id, VersionRequest request)
    {
        var order = await db.PurchaseOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id) ?? throw new ApiProblemException(404, "Purchase order not found.");
        OperationRules.CheckVersion(order, request.RowVersion);
        audit.Record("PurchaseOrderPrintPrepared", "PurchaseOrder", id.ToString(), newValues: new { order.Number, order.Revision });
        await db.SaveChangesAsync();
    }
    private void Snapshot(PurchaseOrder order, string action)
    {
        order.Revision += 1;
        // Safe business document snapshot, with product identifiers/labels at this revision. No supplier contact PII.
        var revision = new PurchaseOrderRevision { PurchaseOrderId = order.Id, Revision = order.Revision, Action = action, ActorId = audit.ActorId,
            Snapshot = JsonSerializer.Serialize(new { order.Number, order.SupplierId, Supplier = order.Supplier.Name, order.OrderDate, order.ExpectedDeliveryDate,
                order.Remarks, order.Status, Lines = order.Lines.OrderBy(x => x.Position).Select(x => new { x.ProductId, Sku = x.Product.Code, Name = x.Product.Name, x.Quantity }) }) };
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
    private IQueryable<PurchaseOrder> Query() => db.PurchaseOrders.Include(x => x.Supplier).Include(x => x.Lines).ThenInclude(x => x.Product).AsSplitQuery();
    private static OrderDto ToDto(PurchaseOrder x) => new(x.Id, x.Number, x.SupplierId, x.Supplier.Code, x.Supplier.Name,
        x.OrderDate, x.ExpectedDeliveryDate, x.Remarks, x.Status, x.Revision, Convert.ToBase64String(x.RowVersion), x.CreatedAt, x.UpdatedAt, x.CreatedBy, x.UpdatedBy,
        x.Lines.OrderBy(l => l.Position).Select(l => new OrderLineDto(l.Id, l.ProductId, l.Product.Code, l.Product.Name, l.Quantity)).ToArray());
}
