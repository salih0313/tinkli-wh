using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Wros.Api.Modules.Audit;
using Wros.Api.Modules.Identity;
using Wros.Api.Modules.Suppliers;
using Wros.Api.Shared.Errors;
using Wros.Api.Shared.Operations;
using Wros.Api.Shared.Persistence;
namespace Wros.Api.Modules.Purchasing.SupplierCatalog;

public sealed class SupplierCatalogService(WrosDbContext db, AuditService audit, IOptions<SupplierImportOptions> settings)
{
    public static readonly string[] Fields = ["Code", "Name", "Brand", "Barcode", "Rsp", "SupplierPrice", "DiscountType", "DiscountValue", "AvailableQuantity", "SupplierCode", "SupplierName"];
    public async Task<PageDto<CatalogDto>> Search(Guid supplierId, int page, int pageSize, string? search, Guid? importId)
    {
        await Supplier(supplierId);
        var query = db.SupplierProducts.AsNoTracking().Where(x => x.SupplierId == supplierId);
        if (importId.HasValue) query = query.Where(x => db.SupplierStockImportRows.Any(r => r.ImportId == importId && r.SupplierProductId == x.Id));
        if (!string.IsNullOrWhiteSpace(search)) { var term = OperationRules.Required(search, 100, "Search"); query = query.Where(x => x.Code.Contains(term) || x.Name.Contains(term) || (x.Barcode != null && x.Barcode.Contains(term))); }
        var count = await query.CountAsync();
        var rows = await query.OrderBy(x => x.Code).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync();
        return new(rows.Select(Dto).ToArray(), page, pageSize, count);
    }
    public async Task<CatalogDto> Save(Guid supplierId, Guid? id, CatalogRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await Supplier(supplierId, true);
        var item = id.HasValue ? await db.SupplierProducts.SingleOrDefaultAsync(x => x.Id == id && x.SupplierId == supplierId) ?? throw new ApiProblemException(404, "Supplier product not found.") : new SupplierProduct { SupplierId = supplierId, CreatedBy = audit.ActorId };
        if (id.HasValue) OperationRules.CheckVersion(item, request.RowVersion);
        var before = id.HasValue ? Dto(item) : null;
        var valid = Validate(request);
        if (id.HasValue && valid.Code != item.Code) throw new ApiProblemException(400, "Supplier product code is a stable identity and cannot be changed.");
        if (valid.ProductId.HasValue && !await db.Products.AnyAsync(x => x.Id == valid.ProductId && x.IsActive)) throw new ApiProblemException(400, "Map to an active WROS product.");
        Apply(item, valid); item.ProductId = valid.ProductId; item.IsActive = valid.IsActive;
        if (!id.HasValue) db.SupplierProducts.Add(item);
        audit.Record(id.HasValue ? "SupplierProductUpdated" : "SupplierProductCreated", "SupplierProduct", item.Id.ToString(), before, Dto(item));
        await db.SaveChangesAsync(); await tx.CommitAsync(); return Dto(item);
    }
    public async Task<ImportPreview> Upload(Guid supplierId, IFormFile file)
    {
        await Supplier(supplierId, true);
        var limits = settings.Value;
        if (file.Length < 1 || file.Length > limits.MaxFileBytes || !limits.AllowedExtensions.Contains(Path.GetExtension(file.FileName), StringComparer.OrdinalIgnoreCase)) throw new ApiProblemException(400, $"Upload an .xlsx file up to {limits.MaxFileBytes} bytes.");
        using var memory = new MemoryStream();
        await file.CopyToAsync(memory); memory.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(memory)); memory.Position = 0;
        if (await db.SupplierStockImports.AnyAsync(x => x.SupplierId == supplierId && x.FileHash == hash && x.ConfirmedAt != null)) throw new ApiProblemException(409, "This file has already been imported for this supplier. See import history.");
        var sheet = XlsxFile.Read(memory, limits);
        var import = new SupplierStockImport { SupplierId = supplierId, FileHash = hash, FileName = Path.GetFileName(file.FileName)[..Math.Min(Path.GetFileName(file.FileName).Length, 255)], CreatedBy = audit.ActorId, UpdatedBy = audit.ActorId,
            HeadersJson = JsonSerializer.Serialize(sheet.Headers), MappingJson = JsonSerializer.Serialize(AutoMap(sheet.Headers)) };
        import.Rows = sheet.Rows.Select(r => new SupplierStockImportRow { ImportId = import.Id, RowNumber = r.Number, CellsJson = JsonSerializer.Serialize(r.Cells) }).ToList();
        db.SupplierStockImports.Add(import);
        audit.Record("SupplierImportUploaded", "SupplierStockImport", import.Id.ToString(), newValues: new { supplierId, Rows = import.Rows.Count });
        await db.SaveChangesAsync(); return await Preview(import);
    }
    public async Task<ImportPreview> GetPreview(Guid supplierId, Guid id) => await Preview(await Import(supplierId, id));
    public async Task<ImportPreview> Map(Guid supplierId, Guid id, MappingRequest request)
    {
        var import = await Import(supplierId, id); Editable(import); OperationRules.CheckVersion(import, request.RowVersion);
        var headers = JsonSerializer.Deserialize<string[]>(import.HeadersJson)!;
        if (request.Mapping is null || request.Mapping.Any(x => !Fields.Contains(x.Key) || x.Value < 0 || x.Value >= headers.Length) || request.Mapping.Values.Distinct().Count() != request.Mapping.Count) throw new ApiProblemException(400, "Map each column to at most one recognized field.");
        import.MappingJson = JsonSerializer.Serialize(request.Mapping); import.UpdatedAt = DateTimeOffset.UtcNow;
        audit.Record("SupplierImportMappingUpdated", "SupplierStockImport", id.ToString(), newValues: new { Fields = request.Mapping.Keys.Order().ToArray() });
        await db.SaveChangesAsync(); return await Preview(import);
    }
    public async Task<CatalogDto[]> Confirm(Guid supplierId, Guid id, ImportConfirmRequest request)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var import = await Import(supplierId, id);
        if (import.ConfirmedAt is not null)
        {
            return import.Rows.OrderBy(x => x.RowNumber).Select(x => JsonSerializer.Deserialize<CatalogDto>(x.ResultJson!)!).ToArray();
        }
        Editable(import); OperationRules.CheckVersion(import, request.RowVersion); await Supplier(supplierId, true);
        if (await db.SupplierStockImports.AnyAsync(x => x.SupplierId == supplierId && x.FileHash == import.FileHash && x.ConfirmedAt != null)) throw new ApiProblemException(409, "This file has already been confirmed.");
        var preview = await Preview(import);
        if (preview.InvalidRows != 0) throw new ApiProblemException(400, "Resolve every validation issue before confirming. No rows have been imported.");
        var codes = preview.Rows.Select(x => x.Product!.Code).ToArray();
        var existing = await db.SupplierProducts.Where(x => x.SupplierId == supplierId && codes.Contains(x.Code)).ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var result = new List<SupplierProduct>();
        var storedRows = import.Rows.ToDictionary(x => x.RowNumber);
        foreach (var row in preview.Rows)
        {
            var data = row.Product!; var found = existing.TryGetValue(data.Code, out var product);
            product ??= new SupplierProduct { SupplierId = supplierId, CreatedBy = audit.ActorId };
            var before = found ? Dto(product) : null;
            Apply(product, data); product.LatestImportId = id;
            if (!found) db.SupplierProducts.Add(product);
            var stored = storedRows[row.RowNumber];
            stored.SupplierProductId = product.Id;
            audit.Record(found ? "SupplierProductImportedUpdate" : "SupplierProductImportedCreate", "SupplierProduct", product.Id.ToString(), before, Dto(product));
            result.Add(product);
        }
        import.ConfirmedAt = DateTimeOffset.UtcNow; import.UpdatedAt = DateTimeOffset.UtcNow;
        audit.Record("SupplierImportConfirmed", "SupplierStockImport", id.ToString(), newValues: new { supplierId, Rows = result.Count });
        await db.SaveChangesAsync();
        // Freeze the confirmation response after SQL generated catalog row versions. A retry returns this result,
        // even if a later import has changed the live catalog; stale versions require an explicit PO price refresh.
        foreach (var product in result)
            import.Rows.Single(x => x.SupplierProductId == product.Id).ResultJson = JsonSerializer.Serialize(Dto(product));
        await db.SaveChangesAsync(); await tx.CommitAsync(); return result.Select(Dto).ToArray();
    }
    public async Task<PageDto<ImportHistoryDto>> History(Guid supplierId, int page, int pageSize)
    {
        await Supplier(supplierId);
        var query = db.SupplierStockImports.AsNoTracking().Where(x => x.SupplierId == supplierId);
        var total = await query.CountAsync();
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new ImportHistoryDto(x.Id, x.FileName, x.CreatedAt, x.CreatedBy, x.ConfirmedAt, x.Rows.Count)).ToArrayAsync();
        return new(rows, page, pageSize, total);
    }
    private async Task<ImportPreview> Preview(SupplierStockImport import)
    {
        var supplier = await Supplier(import.SupplierId);
        var mapping = JsonSerializer.Deserialize<Dictionary<string, int>>(import.MappingJson)!;
        var headers = JsonSerializer.Deserialize<string[]>(import.HeadersJson)!;
        var rows = new List<ImportPreviewRow>();
        var cellRows = import.Rows.ToDictionary(x => x.Id, x => JsonSerializer.Deserialize<string[]>(x.CellsJson)!);
        var codes = import.Rows.Select(x => Read(x, "Code").ToUpperInvariant()).Distinct().ToArray();
        var existing = (await db.SupplierProducts.AsNoTracking().Where(x => x.SupplierId == import.SupplierId && codes.Contains(x.Code)).Select(x => x.Code).ToArrayAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var duplicateCodes = import.Rows.Select(r => Read(r, "Code").ToUpperInvariant()).Where(x => x.Length > 0).GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        string Read(SupplierStockImportRow row, string field) { var cells = cellRows[row.Id]; return mapping.TryGetValue(field, out var col) && col >= 0 && col < cells.Length ? cells[col].Trim() : ""; }
        foreach (var row in import.Rows.OrderBy(x => x.RowNumber))
        {
            var errors = new List<string>(); CatalogRequest? product = null; decimal? net = null;
            try
            {
                foreach (var field in mapping.Keys)
                    if (Read(row, field).StartsWith(XlsxFile.InvalidCellPrefix, StringComparison.Ordinal)) errors.Add($"{field}: {Read(row, field)[XlsxFile.InvalidCellPrefix.Length..]}");
                foreach (var field in new[] { "Code", "Name", "SupplierPrice" }) if (!mapping.ContainsKey(field)) errors.Add($"Map required field {field}.");
                if (headers.Any(h => Normalize(h) is "suppliercode" or "vendorcode") && !mapping.ContainsKey("SupplierCode")) errors.Add("Map the supplier code column for validation.");
                if (headers.Any(h => Normalize(h) is "suppliername" or "supplier" or "vendorname") && !mapping.ContainsKey("SupplierName")) errors.Add("Map the supplier name column for validation.");
                // Recognizable supplier identity columns cannot be bypassed by mapping another column instead.
                for (var c = 0; c < headers.Length; c++)
                {
                    var header = Normalize(headers[c]); var actual = cellRows[row.Id][c];
                    if ((header is "suppliercode" or "vendorcode") && !actual.Equals(supplier.Code, StringComparison.OrdinalIgnoreCase)) errors.Add($"Supplier code in column {c + 1} does not match selected supplier.");
                    if ((header is "suppliername" or "supplier" or "vendorname") && !actual.Equals(supplier.Name, StringComparison.OrdinalIgnoreCase)) errors.Add($"Supplier name in column {c + 1} does not match selected supplier.");
                }
                var sc = Read(row, "SupplierCode"); var sn = Read(row, "SupplierName");
                if (mapping.ContainsKey("SupplierCode") && !sc.Equals(supplier.Code, StringComparison.OrdinalIgnoreCase)) errors.Add("Supplier code does not match selected supplier.");
                if (mapping.ContainsKey("SupplierName") && !sn.Equals(supplier.Name, StringComparison.OrdinalIgnoreCase)) errors.Add("Supplier name does not match selected supplier.");
                if (duplicateCodes.Contains(Read(row, "Code").ToUpperInvariant())) errors.Add("Duplicate supplier product code in file; correct the file and upload again.");
                decimal? Number(string field, bool optional)
                {
                    var text = Read(row, field); if (optional && text == "") return null;
                    if (field == "DiscountValue") text = text.TrimEnd('%').Trim();
                    if (!decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)) throw new ApiProblemException(400, $"{field} must be an unambiguous number using a decimal point, without currency symbols or thousands separators.");
                    return value;
                }
                var typeText = Read(row, "DiscountType"); var dv = Read(row, "DiscountValue");
                if (dv.Count(c => c == '%') > 1 || (dv.Contains('%') && !dv.EndsWith('%'))) errors.Add("Ambiguous discount value. Use a single trailing percentage symbol or a plain number.");
                if (typeText == "" && mapping.TryGetValue("DiscountValue", out var discountColumn))
                {
                    var header = Normalize(headers[discountColumn]);
                    if (header is "discountpercent" or "discountpercentage" || headers[discountColumn].Contains('%')) typeText = "Percentage";
                    else if (header == "discountamount") typeText = "Fixed";
                }
                var type = typeText.ToLowerInvariant() switch { "" when dv.EndsWith('%') => "Percentage", "" or "none" => "None", "%" or "percent" or "percentage" => "Percentage", "fixed" or "amount" => "Fixed", _ => throw new ApiProblemException(400, "Ambiguous discount type. Use None, Percentage or Fixed.") };
                if (dv.EndsWith('%') && type != "Percentage") errors.Add("Percentage value conflicts with discount type.");
                product = Validate(new(Read(row, "Code"), Read(row, "Name"), Read(row, "Brand"), Read(row, "Barcode"), Number("Rsp", true), Number("SupplierPrice", false)!.Value, type, Number("DiscountValue", true) ?? 0, Number("AvailableQuantity", true), null, true, null));
                net = PurchasePricing.Calculate(product.SupplierPrice, product.DiscountType, product.DiscountValue).Net;
            }
            catch (ApiProblemException ex) { errors.Add(ex.Message); }
            rows.Add(new(row.RowNumber, product, net, existing.Contains(Read(row, "Code")), errors.ToArray()));
        }
        return new(import.Id, import.FileName, headers, mapping, Convert.ToBase64String(import.RowVersion), rows.Count, rows.Count(x => x.Errors.Length == 0), rows.Count(x => x.Errors.Length != 0), rows.Count(x => !x.Existing && x.Errors.Length == 0), rows.Count(x => x.Existing && x.Errors.Length == 0), rows.ToArray(), import.ConfirmedAt != null);
    }
    private async Task<SupplierStockImport> Import(Guid supplierId, Guid id)
    {
        var item = await db.SupplierStockImports.Include(x => x.Rows).SingleOrDefaultAsync(x => x.Id == id && x.SupplierId == supplierId) ?? throw new ApiProblemException(404, "Import not found.");
        if (item.CreatedBy != audit.ActorId) throw new ApiProblemException(403, "Only the uploading user can preview or confirm this import.");
        return item;
    }
    private void Editable(SupplierStockImport item)
    {
        if (item.ConfirmedAt != null) throw new ApiProblemException(409, "This import is already confirmed.");
        if (item.CreatedAt.AddHours(settings.Value.PreviewHours) < DateTimeOffset.UtcNow) throw new ApiProblemException(409, "Preview expired. Upload again.");
    }
    private async Task<Supplier> Supplier(Guid id, bool active = false) => await db.Suppliers.SingleOrDefaultAsync(x => x.Id == id && (!active || x.IsActive)) ?? throw new ApiProblemException(400, "Choose a valid, active supplier.");
    private static CatalogRequest Validate(CatalogRequest r)
    {
        var code = OperationRules.Required(r.Code, 64, "Supplier product code").ToUpperInvariant();
        var name = OperationRules.Required(r.Name, 200, "Product name");
        var pricing = PurchasePricing.Calculate(r.SupplierPrice, r.DiscountType, r.DiscountValue);
        if (r.Rsp is < 0 or > PurchasePricing.MaximumPrice || (r.Rsp.HasValue && r.Rsp != PurchasePricing.Money(r.Rsp.Value))) throw new ApiProblemException(400, "RSP must be non-negative with up to three decimal places.");
        if (r.AvailableQuantity.HasValue) PurchaseOrderService.Quantity(r.AvailableQuantity.Value, true);
        return r with { Code = code, Name = name, Brand = OperationRules.Optional(r.Brand, 200, "Brand"), Barcode = OperationRules.Optional(r.Barcode, 100, "Barcode"), DiscountType = pricing.Type };
    }
    private void Apply(SupplierProduct p, CatalogRequest r)
    {
        p.Code = r.Code; p.Name = r.Name; p.Brand = r.Brand; p.Barcode = r.Barcode; p.Rsp = r.Rsp;
        p.SupplierPrice = r.SupplierPrice; p.DiscountType = r.DiscountType!; p.DiscountValue = r.DiscountValue;
        p.NetUnitPrice = PurchasePricing.Calculate(r.SupplierPrice, r.DiscountType, r.DiscountValue).Net;
        p.AvailableQuantity = r.AvailableQuantity; p.UpdatedAt = DateTimeOffset.UtcNow; p.UpdatedBy = audit.ActorId;
    }
    public static CatalogDto Dto(SupplierProduct p) => new(p.Id, p.SupplierId, p.Code, p.ProductId, p.Name, p.Brand, p.Barcode,
        p.Rsp, p.SupplierPrice, p.DiscountType, p.DiscountValue, p.NetUnitPrice, p.AvailableQuantity, p.LatestImportId, p.IsActive, p.UpdatedAt, Convert.ToBase64String(p.RowVersion));
    private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    private static Dictionary<string, int> AutoMap(string[] headers)
    {
        var aliases = new Dictionary<string, string[]> {
            ["Code"] = ["supplierproductcode", "productcode", "itemcode", "suppliersku", "sku"], ["Name"] = ["productname", "itemname", "description", "name"],
            ["Brand"] = ["brand", "manufacturer"], ["Barcode"] = ["barcode", "ean", "upc"], ["Rsp"] = ["rspmrp", "rsp", "mrp", "retailprice"],
            ["SupplierPrice"] = ["supplierpurchaseprice", "supplierprice", "purchaseprice", "grossunitprice"], ["DiscountType"] = ["discounttype"],
            ["DiscountValue"] = ["discountvalue", "discountpercent", "discountpercentage", "discountamount", "discount"], ["AvailableQuantity"] = ["supplieravailablequantity", "availablequantity", "stockonhand", "soh", "stock"],
            ["SupplierCode"] = ["suppliercode", "vendorcode"], ["SupplierName"] = ["suppliername", "vendorname", "supplier"] };
        var result = new Dictionary<string, int>();
        foreach (var pair in aliases) { var matches = headers.Select((h, index) => (h, index)).Where(x => pair.Value.Contains(Normalize(x.h))).ToArray(); if (matches.Length == 1 && !result.ContainsValue(matches[0].index)) result[pair.Key] = matches[0].index; }
        return result;
    }
}
