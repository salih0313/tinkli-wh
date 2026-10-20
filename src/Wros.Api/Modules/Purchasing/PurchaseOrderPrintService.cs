using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
namespace Wros.Api.Modules.Purchasing;
public sealed class PurchaseOrderPrintOptions
{
    public string PageSize { get; set; } = "A4";
    [Range(8, 25)] public int MarginMillimeters { get; set; } = 12;
    public string? HeaderAssetPath { get; set; }
    public string? FooterAssetPath { get; set; }
}
// Presentation consumes saved, authoritative DTOs. Header/footer assets never participate in calculations.
public sealed class PurchaseOrderPrintService(IOptions<PurchaseOrderPrintOptions> settings, IWebHostEnvironment environment)
{
    public string Render(OrderDto order)
    {
        var options = settings.Value;
        static string E(string? value) => WebUtility.HtmlEncode(value ?? "—");
        static string M(decimal value) => value.ToString("N3", CultureInfo.InvariantCulture);
        var html = new StringBuilder($$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><title>{{E(order.Number)}}</title><style>
            @page { size:A4 portrait; margin:{{options.MarginMillimeters}}mm; }
            * { box-sizing:border-box } body { font:11px Arial,sans-serif;color:#162033;margin:0 }
            header { border-bottom:2px solid #23354f;padding-bottom:14px } h1 { font-size:22px;margin:12px 0 6px }
            .facts { display:flex;gap:30px;flex-wrap:wrap;margin:18px 0 } .facts p { margin:4px 0 }
            table { width:100%;border-collapse:collapse;table-layout:fixed;font-size:9px } th,td { border-bottom:1px solid #dce0e6;padding:7px 3px;vertical-align:top;overflow-wrap:anywhere }
            th { text-align:left;background:#eef1f5 } .num { text-align:right;font-variant-numeric:tabular-nums }
            thead { display:table-header-group } tr { break-inside:avoid } .totals { margin:18px 0 18px auto;width:270px;break-inside:avoid;font-size:12px }
            .totals p { display:flex;justify-content:space-between;gap:16px } .final { border-top:2px solid #23354f;padding-top:10px;font-weight:bold }
            .remarks { white-space:pre-wrap;overflow-wrap:anywhere } footer { border-top:1px solid #ddd;margin-top:24px;padding-top:10px;font-size:9px;color:#637087 }
            .branding { max-width:100%;max-height:24mm;object-fit:contain } .actions { margin:12px 0 } @media print { .actions { display:none } }
            </style></head><body><div class="actions"><button onclick="window.print()">Print / Save PDF</button></div>
            <header>{{Branding(options.HeaderAssetPath, "WROS · Purchase order")}}<h1>{{E(order.Number)}}</h1><div>Revision {{order.Revision}} · {{E(order.Status)}} · KWD</div></header>
            <section class="facts"><div><p><strong>Supplier</strong></p><p>{{E(order.SupplierCode)}} · {{E(order.SupplierName)}}</p></div><div><p><strong>Order date</strong> {{order.OrderDate:yyyy-MM-dd}}</p><p><strong>Expected delivery</strong> {{E(order.ExpectedDeliveryDate?.ToString("yyyy-MM-dd"))}}</p></div></section>
            <table><colgroup><col style="width:12%"><col style="width:25%"><col style="width:10%"><col style="width:12%"><col style="width:8%"><col style="width:11%"><col style="width:11%"><col style="width:11%"></colgroup><thead><tr><th>Product code</th><th>Product / brand / barcode</th><th class="num">Quantity</th><th class="num">Supplier unit price</th><th class="num">Discount</th><th class="num">Net unit price</th><th class="num">RSP/MRP</th><th class="num">Line total</th></tr></thead><tbody>
            """);
        foreach (var line in order.Lines)
        {
            var p = line.Pricing;
            var discount = p is null ? "—" : p.DiscountType == "Percentage" ? M(p.DiscountValue) + "%" : p.DiscountType == "Fixed" ? M(p.DiscountValue) : "—";
            html.Append($"<tr><td>{E(line.Sku)}</td><td>{E(line.Name)}<br>{E(p?.Brand)}<br>{E(p?.Barcode)}</td><td class='num'>{line.Quantity:0}</td><td class='num'>{(p is null ? "—" : M(p.SupplierPrice))}</td><td class='num'>{discount}</td><td class='num'>{(p is null ? "—" : M(p.NetUnitPrice))}</td><td class='num'>{(p?.Rsp is null ? "—" : M(p.Rsp.Value))}</td><td class='num'>{(p is null ? "—" : M(p.LineTotal))}</td></tr>");
        }
        html.Append("</tbody></table>");
        if (order.Summary.UnpricedLines == 0)
            html.Append($"<section class='totals'><p><span>Gross total</span><span>{M(order.Summary.GrossAmount)}</span></p><p><span>Total discount</span><span>{M(order.Summary.DiscountAmount)}</span></p><p class='final'><span>Final net total (KWD)</span><span>{M(order.Summary.NetAmount)}</span></p></section>");
        else html.Append("<p>This historical purchase order contains unpriced lines. A complete financial total is unavailable.</p>");
        html.Append($"<p class='remarks'>{E(order.Remarks)}</p><footer>{Branding(options.FooterAssetPath, "WROS · Supplier request. Actual receipt is recorded separately on delivery notes.")}</footer></body></html>");
        return html.ToString();
    }
    private string Branding(string? path, string placeholder)
    {
        if (string.IsNullOrWhiteSpace(path)) return WebUtility.HtmlEncode(placeholder);
        // Configuration points only to local image assets under PrintAssets, never arbitrary files or URLs.
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "PrintAssets")) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, path));
        var extension = Path.GetExtension(full).ToLowerInvariant();
        if (!full.StartsWith(root, StringComparison.Ordinal) || extension is not (".png" or ".jpg" or ".jpeg") || !File.Exists(full) || new FileInfo(full).Length > 1048576)
            throw new Wros.Api.Shared.Errors.ApiProblemException(503, "PO print branding configuration is invalid. Use a PNG/JPEG image up to 1 MB under PrintAssets.");
        return $"<img class='branding' alt='{WebUtility.HtmlEncode(placeholder)}' src='data:image/{(extension == ".png" ? "png" : "jpeg")};base64,{Convert.ToBase64String(File.ReadAllBytes(full))}'>";
    }
}
