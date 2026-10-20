using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Wros.Api.Shared.Operations;
namespace Wros.Api.Modules.Purchasing.SupplierCatalog;

public sealed class SupplierProduct : OperationalRecord
{
    public Guid SupplierId { get; set; }
    [MaxLength(64)] public string Code { get; set; } = "";
    public Guid? ProductId { get; set; }
    [MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(200)] public string? Brand { get; set; }
    [MaxLength(100)] public string? Barcode { get; set; }
    public decimal? Rsp { get; set; }
    public decimal SupplierPrice { get; set; }
    [MaxLength(16)] public string DiscountType { get; set; } = "None";
    public decimal DiscountValue { get; set; }
    public decimal NetUnitPrice { get; set; }
    public decimal? AvailableQuantity { get; set; }
    public Guid? LatestImportId { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class SupplierStockImport : OperationalRecord
{
    public Guid SupplierId { get; set; }
    [MaxLength(64)] public string FileHash { get; set; } = "";
    [MaxLength(255)] public string FileName { get; set; } = "";
    public string HeadersJson { get; set; } = "[]";
    public string MappingJson { get; set; } = "{}";
    public DateTimeOffset? ConfirmedAt { get; set; }
    public List<SupplierStockImportRow> Rows { get; set; } = [];
}
public sealed class SupplierStockImportRow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImportId { get; set; }
    public int RowNumber { get; set; }
    public string CellsJson { get; set; } = "[]";
    public string? ResultJson { get; set; }
    public Guid? SupplierProductId { get; set; }
}
public sealed class SupplierImportOptions
{
    [Range(1024, 10485760)] public int MaxFileBytes { get; set; } = 5242880;
    [Range(1048576, 67108864)] public int MaxExpandedBytes { get; set; } = 33554432;
    [Range(1, 5000)] public int MaxRows { get; set; } = 2000;
    [Range(9, 128)] public int MaxColumns { get; set; } = 64;
    // Configuration binding appends to initialized arrays; keep this empty and use appsettings defaults.
    public string[] AllowedExtensions { get; set; } = [];
    public string DefaultCurrency { get; set; } = "KWD";
    [Range(1, 72)] public int PreviewHours { get; set; } = 24;
}
public sealed record CatalogRequest(string Code, string Name, string? Brand, string? Barcode, decimal? Rsp,
    decimal SupplierPrice, string? DiscountType, decimal DiscountValue, decimal? AvailableQuantity,
    Guid? ProductId, bool IsActive, string? RowVersion);
public sealed record CatalogDto(Guid Id, Guid SupplierId, string Code, Guid? ProductId, string Name, string? Brand,
    string? Barcode, decimal? Rsp, decimal SupplierPrice, string DiscountType, decimal DiscountValue,
    decimal NetUnitPrice, decimal? AvailableQuantity, Guid? LatestImportId, bool IsActive, DateTimeOffset UpdatedAt, string RowVersion);
public sealed record MappingRequest(Dictionary<string, int> Mapping, string RowVersion);
public sealed record ImportConfirmRequest(string RowVersion);
public sealed record ImportPreviewRow(int RowNumber, CatalogRequest? Product, decimal? NetUnitPrice, bool Existing, string[] Errors);
public sealed record ImportPreview(Guid Id, string FileName, string[] Headers, Dictionary<string, int> Mapping,
    string RowVersion, int TotalRows, int ValidRows, int InvalidRows, int NewProducts, int ExistingProducts,
    ImportPreviewRow[] Rows, bool Confirmed);
public sealed record ImportHistoryDto(Guid Id, string FileName, DateTimeOffset CreatedAt, Guid? CreatedBy,
    DateTimeOffset? ConfirmedAt, int RowCount);
public sealed record PricingSnapshot(Guid? SupplierProductId, string SupplierProductCode, string ProductName,
    string? Brand, string? Barcode, decimal? Rsp, decimal SupplierPrice, string DiscountType,
    decimal DiscountValue, decimal NetUnitPrice, decimal Quantity, decimal GrossAmount, decimal DiscountAmount, decimal LineTotal)
{
    public string LineTotalText => LineTotal.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
}
public sealed record PricingSummary(int SelectedProducts, decimal TotalQuantity, decimal GrossAmount,
    decimal DiscountAmount, decimal NetAmount, int UnpricedLines)
{
    public string GrossAmountText => GrossAmount.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
    public string DiscountAmountText => DiscountAmount.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
    public string NetAmountText => NetAmount.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);
    public string TotalQuantityText => TotalQuantity.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
}
public static class PurchasePricing
{
    // Unit prices are rounded first, then multiplied by whole-unit quantities. Never discount RSP.
    public const decimal MaximumPrice = 999999999m;
    public static decimal Money(decimal value) => decimal.Round(value, 3, MidpointRounding.AwayFromZero);
    public static (string Type, decimal Net) Calculate(decimal price, string? type, decimal value)
    {
        type = string.IsNullOrWhiteSpace(type) ? "None" : type.Trim();
        if (price < 0 || price > MaximumPrice || price != Money(price)) Fail("Supplier price must be non-negative, at most 999999999 KWD, with up to three decimal places.");
        if (value < 0 || value != Money(value)) Fail("Discount must be non-negative with up to three decimal places.");
        if (type is not ("None" or "Percentage" or "Fixed")) Fail("Discount type must be None, Percentage or Fixed.");
        if (type == "None" && value != 0) Fail("Choose a discount type for a non-zero discount.");
        if (type == "Percentage" && value > 100) Fail("Percentage discount must be between 0 and 100.");
        if (type == "Fixed" && value > price) Fail("Fixed discount cannot exceed supplier price.");
        return (type, Money(type == "Percentage" ? price * (1 - value / 100) : type == "Fixed" ? price - value : price));
    }
    public static PricingSnapshot Snapshot(SupplierProduct p, decimal quantity) => new(p.Id, p.Code, p.Name,
        p.Brand, p.Barcode, p.Rsp, p.SupplierPrice, p.DiscountType, p.DiscountValue, p.NetUnitPrice,
        quantity, Money(p.SupplierPrice * quantity), Money((p.SupplierPrice - p.NetUnitPrice) * quantity), Money(p.NetUnitPrice * quantity));
    private static void Fail(string message) => throw new Wros.Api.Shared.Errors.ApiProblemException(400, message);
}
public static class SupplierCatalogConfiguration
{
    public static void Apply(ModelBuilder model)
    {
        var p = model.Entity<SupplierProduct>(); OperationRules.Configure(p);
        p.HasIndex(x => new { x.SupplierId, x.Code }).IsUnique();
        p.HasIndex(x => new { x.SupplierId, x.LatestImportId });
        p.HasOne<Wros.Api.Modules.Suppliers.Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        p.HasOne<Wros.Api.Modules.Catalog.Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        p.HasOne<SupplierStockImport>().WithMany().HasForeignKey(x => x.LatestImportId).OnDelete(DeleteBehavior.Restrict);
        p.Property(x => x.Rsp).HasPrecision(18, 3);
        p.Property(x => x.SupplierPrice).HasPrecision(18, 3); p.Property(x => x.DiscountValue).HasPrecision(18, 3);
        p.Property(x => x.NetUnitPrice).HasPrecision(18, 3); p.Property(x => x.AvailableQuantity).HasPrecision(18, 0);
        p.ToTable(t => { t.HasCheckConstraint("CK_SupplierProduct_Price", "[SupplierPrice]>=0 AND [NetUnitPrice]>=0 AND ([Rsp] IS NULL OR [Rsp]>=0) AND [DiscountValue]>=0"); });
        var i = model.Entity<SupplierStockImport>(); OperationRules.Configure(i);
        i.HasIndex(x => new { x.SupplierId, x.CreatedAt });
        i.HasIndex(x => new { x.SupplierId, x.FileHash }).IsUnique().HasFilter("[ConfirmedAt] IS NOT NULL");
        i.HasOne<Wros.Api.Modules.Suppliers.Supplier>().WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
        i.HasMany(x => x.Rows).WithOne().HasForeignKey(x => x.ImportId).OnDelete(DeleteBehavior.Restrict);
        var r = model.Entity<SupplierStockImportRow>(); r.HasKey(x => x.Id);
        r.HasIndex(x => new { x.ImportId, x.RowNumber }).IsUnique();
        r.HasOne<SupplierProduct>().WithMany().HasForeignKey(x => x.SupplierProductId).OnDelete(DeleteBehavior.Restrict);
    }
}
