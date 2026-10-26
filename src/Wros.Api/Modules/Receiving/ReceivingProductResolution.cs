using Microsoft.EntityFrameworkCore;
using Wros.Api.Modules.Catalog;
using Wros.Api.Shared.Errors;

namespace Wros.Api.Modules.Receiving;

public sealed partial class ReceivingService
{
    // Called inside the receipt's serializable transaction. Product and supplier-code unique
    // indexes protect concurrent resolution; a failed receipt rolls back creation and mapping too.
    private async Task<ReceiptLineRequest[]> ResolveReceiptProducts(Guid supplierId, ReceiptLineRequest[] lines)
    {
        var catalogIds = lines.Where(x => x.SupplierProductId.HasValue).Select(x => x.SupplierProductId!.Value).Distinct().ToArray();
        var catalog = await db.SupplierProducts.Where(x => catalogIds.Contains(x.Id) && x.SupplierId == supplierId && x.IsActive).ToDictionaryAsync(x => x.Id);
        if (catalog.Count != catalogIds.Length) throw new ApiProblemException(400, "Choose active supplier products belonging to this receipt's supplier.");
        if (lines.Any(x => !x.ProductId.HasValue && !x.SupplierProductId.HasValue)) throw new ApiProblemException(400, "Choose a product or a supplier catalog item for every receipt line.");

        var explicitIds = lines.Where(x => x.ProductId.HasValue).Select(x => x.ProductId!.Value).Distinct().ToArray();
        var explicitProducts = await db.Products.Where(x => explicitIds.Contains(x.Id) && x.IsActive).ToDictionaryAsync(x => x.Id);
        if (explicitProducts.Count != explicitIds.Length) throw new ApiProblemException(400, "Every selected receipt product must be active.");
        var codes = lines.Where(x => !x.ProductId.HasValue).Select(x => catalog[x.SupplierProductId!.Value].Code).Distinct().ToArray();
        var byCode = (await db.Products.Where(x => codes.Contains(x.Code)).ToArrayAsync()).ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var resolved = new List<ReceiptLineRequest>();
        foreach (var line in lines)
        {
            if (line.ProductId.HasValue) { resolved.Add(line); continue; }
            var item = catalog[line.SupplierProductId!.Value];
            if (!byCode.TryGetValue(item.Code, out var product))
            {
                product = new Product { Code = item.Code, Name = item.Name, Brand = item.Brand, Barcode = item.Barcode,
                    CreatedBy = audit.ActorId, UpdatedBy = audit.ActorId };
                db.Products.Add(product); byCode.Add(item.Code, product);
                audit.Record("ProductCreatedFromReceiving", "Product", product.Id.ToString(), newValues: new { product.Code, product.Name, product.Brand, product.Barcode, product.TrackExpiry });
            }
            if (!product.IsActive) throw new ApiProblemException(400, $"Product code {item.Code} already exists but is inactive. Reactivate it before receiving.");
            if (item.ProductId != product.Id)
            {
                var before = new { item.ProductId };
                item.ProductId = product.Id; item.UpdatedBy = audit.ActorId; item.UpdatedAt = DateTimeOffset.UtcNow;
                audit.Record("SupplierProductMatchedByCode", "SupplierProduct", item.Id.ToString(), before, new { item.ProductId, item.Code });
            }
            resolved.Add(line with { ProductId = product.Id });
        }
        return resolved.ToArray();
    }
}
