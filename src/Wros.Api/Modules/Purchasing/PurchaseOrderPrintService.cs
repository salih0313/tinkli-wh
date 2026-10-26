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
    [MaxLength(200)] public string CompanyName { get; set; } = "TINKLI KIDS PLANET";
    [MaxLength(2000)] public string? CompanyAddress { get; set; }
    [MaxLength(100)] public string? CompanyPhone { get; set; }
    [MaxLength(254)] public string? CompanyEmail { get; set; }
    [MaxLength(200)] public string? CompanyWebsite { get; set; }
    [MaxLength(200)] public string? ShipToName { get; set; }
    [MaxLength(2000)] public string? ShipToAddress { get; set; }
    [MaxLength(200)] public string? ShipToContact { get; set; }
    [MaxLength(500)] public string? PaymentTerms { get; set; }
    [MaxLength(200)] public string? DeliveryMethod { get; set; }
    [MaxLength(1000)] public string RequestMessage { get; set; } = "Please supply the following items as listed below.";
}

// Saved PO snapshots are authoritative for item labels and money. Print contact details
// come from the selected supplier and configured company details, never sample orders.
public sealed class PurchaseOrderPrintService(IOptions<PurchaseOrderPrintOptions> settings, IWebHostEnvironment environment)
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-GB");
    private static string E(string? value) => WebUtility.HtmlEncode(string.IsNullOrWhiteSpace(value) ? "—" : value);
    private static string M(decimal? value) => value?.ToString("N3", CultureInfo.InvariantCulture) ?? "—";
    private static string D(DateOnly value) => value.ToString("dd MMMM yyyy", Culture);
    private static string Detail(string? label, string? value) => string.IsNullOrWhiteSpace(value) ? "" : $"<p>{(label is null ? "" : $"<span>{E(label)}:</span> ")}{E(value)}</p>";

    public string Render(OrderDto order)
    {
        var o = settings.Value;
        var company = $"<strong>{E(o.CompanyName)}</strong>{Detail(null, o.CompanyAddress)}{Detail("Phone", o.CompanyPhone)}<div class='company-links'>{Detail(null, o.CompanyWebsite)}{Detail(null, o.CompanyEmail)}</div>";
        var supplier = $"<strong>{E(order.SupplierName)}</strong>{Detail(null, order.SupplierAddress)}{Detail("Contact", order.SupplierContactPerson)}{Detail("Phone", order.SupplierPhone)}{Detail("Email", order.SupplierEmail)}";
        var shipTo = $"<strong>{E(o.ShipToName ?? o.CompanyName)}</strong>{Detail(null, o.ShipToAddress ?? o.CompanyAddress)}{Detail("Contact", o.ShipToContact)}";
        var logo = string.IsNullOrWhiteSpace(o.HeaderAssetPath)
            ? Image(Path.Combine(AppContext.BaseDirectory, "BrandingAssets", "logo", "logo_full.png"), "company-logo", o.CompanyName)
            : CustomAsset(o.HeaderAssetPath, "header-artwork", o.CompanyName);
        var html = new StringBuilder($$"""
            <!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>{{E(order.Number)}} - Purchase Order</title><style>
            @page { size:A4 portrait; margin:{{o.MarginMillimeters}}mm; }
            * { box-sizing:border-box } body { margin:0;color:#202024;font:10pt Arial,Helvetica,sans-serif;line-height:1.4;background:#edf0f5 }
            .actions { position:sticky;top:0;z-index:10;display:flex;align-items:center;justify-content:space-between;gap:16px;padding:14px 20px;background:#392653;color:#fff;font-size:12px }
            .actions p { margin:0 }.actions button { border:0;border-radius:5px;background:#fff;color:#392653;padding:10px 16px;font:600 13px Arial;cursor:pointer;white-space:nowrap }.actions button:disabled { opacity:.6;cursor:wait }
            .sheet { width:210mm;min-height:297mm;margin:20px auto;padding:{{o.MarginMillimeters}}mm;background:white;box-shadow:0 4px 24px #16284820 }
            p { margin:3px 0;white-space:pre-line;overflow-wrap:anywhere } strong { font-weight:700 }
            .letterhead { display:flex;justify-content:space-between;align-items:flex-start;gap:24px;margin-bottom:5mm;break-inside:avoid }
            .company-logo { width:65mm;max-width:100%;height:auto;max-height:34mm;object-fit:contain;object-position:left top }
            .header-artwork { max-width:100%;max-height:36mm;object-fit:contain }.company { width:76mm;font-size:9pt;line-height:1.35 }.company > strong { display:none }
            .company-links { display:flex;flex-wrap:wrap;gap:4px 14px;border-top:1px solid #9474ad;margin-top:5px;padding-top:3px;font-size:8.5pt;font-weight:600 }
            .company-links:empty { display:none }
            .order-heading { display:flex;justify-content:space-between;align-items:center;gap:20px;margin-bottom:7mm;break-inside:avoid }
            h1 { margin:0;font-size:18pt;letter-spacing:.4px;line-height:1.2 }.reference { width:112mm;border-collapse:collapse;table-layout:fixed;font-size:10pt;text-align:center }
            .reference th,.reference td { border:1px solid #555;padding:6px 8px;overflow-wrap:anywhere }.reference th { background:#96069e;color:#fff;font-weight:600 }.reference col:first-child { width:57% }
            .parties { display:grid;grid-template-columns:1fr 1fr;column-gap:0;margin-bottom:4mm;break-inside:avoid }.party h2 { background:#96069e;color:#fff;border:1px solid #555;margin:0;padding:5px 12px;font-size:11pt;font-weight:500;text-align:center }.party + .party h2 { border-left:0 }
            .party-body { padding:10px 8px 7px;min-height:28mm;font-size:10pt }.terms { margin-top:7px }.intro { text-align:center;border-top:1px solid #9fb5bb;padding-top:9px;margin:0 0 4mm;font-size:10pt;break-after:avoid }
            .items { width:100%;table-layout:fixed;border-collapse:collapse;font-size:8.5pt;line-height:1.35 }.items th,.items td { border:1px solid #777;padding:7px 5px;vertical-align:middle;overflow-wrap:anywhere }.items th { background:#e3e3e3;text-align:center;font-weight:700;line-height:1.25 }
            .items td { text-align:center }.items th:first-child,.items td:first-child { padding-inline:2px }.items .product-name { text-align:left;overflow-wrap:break-word }.items .num { text-align:right;font-variant-numeric:tabular-nums }.items .barcode { font-size:7.5pt }.price-note { display:block;color:#666;font-size:7pt;margin-top:3px }
            thead { display:table-header-group } tr { break-inside:avoid;page-break-inside:avoid } .summary { display:flex;justify-content:space-between;align-items:flex-start;gap:20px;margin-top:5mm;break-inside:avoid }
            .quantity-summary { font-size:9pt;color:#555 }.totals { width:70mm;font-size:10pt;flex-shrink:0 }.totals p { display:flex;justify-content:space-between;gap:12px;margin:0;padding:4px 0 }.totals .final { border-top:2px solid #6147a6;margin-top:3px;padding-top:7px;font-weight:700;font-size:11pt }.partial { border:1px solid #bbb;padding:9px;font-size:9pt }
            .remarks { margin-top:3mm;break-inside:avoid }.remarks h2 { margin:0 0 4px;font-size:10pt }.remarks p { font-size:9pt;line-height:1.5 }
            .document-footer { border-top:3mm solid #8b5aa1;margin-top:6mm;padding-top:3mm;break-inside:avoid;display:flex;justify-content:space-between;gap:20px;font-size:8pt;color:#666 }.footer-artwork { max-width:100%;max-height:15mm;object-fit:contain }
            @media screen and (max-width:850px) { .actions { position:static }.sheet { margin:12px;width:210mm } }
            @media print { body { background:#fff }.actions { display:none }.sheet { width:auto;min-height:0;margin:0;padding:0 .2mm;box-shadow:none }.document-footer { margin-top:6mm } * { -webkit-print-color-adjust:exact;print-color-adjust:exact } }
            </style></head><body><div class="actions"><p>Save this purchase order as a PDF, then attach it to your supplier email.<br>Use A4 paper and turn off the browser's headers and footers.</p><button id="print-button" type="button" onclick="window.print()" disabled>Preparing document…</button></div>
            <main class="sheet"><header class="letterhead"><div>{{logo}}</div><div class="company">{{company}}</div></header>
            <section class="order-heading"><h1>PURCHASE ORDER</h1><table class="reference" aria-label="Purchase order reference"><colgroup><col><col></colgroup><thead><tr><th>Reference No.</th><th>Date</th></tr></thead><tbody><tr><td>{{E(order.Number)}}</td><td>{{D(order.OrderDate)}}</td></tr></tbody></table></section>
            <section class="parties"><div class="party"><h2>TO</h2><div class="party-body">{{supplier}}<div class="terms">{{Detail("Payment", o.PaymentTerms)}}<p><span>Currency:</span> KWD</p></div></div></div>
            <div class="party"><h2>SHIP TO</h2><div class="party-body">{{shipTo}}<div class="terms">{{Detail("Delivery", o.DeliveryMethod)}}{{Detail("Delivery date", order.ExpectedDeliveryDate.HasValue ? D(order.ExpectedDeliveryDate.Value) : null)}}</div></div></div></section>
            <p class="intro">{{E(o.RequestMessage)}}</p>
            <table class="items" aria-label="Requested products"><colgroup><col style="width:4%"><col style="width:8%"><col style="width:12%"><col style="width:11%"><col style="width:25.5%"><col style="width:8%"><col style="width:10%"><col style="width:6%"><col style="width:15.5%"></colgroup>
            <thead><tr><th>No.</th><th>Product<br>code</th><th>Barcode</th><th>Brand</th><th>Product name</th><th>RSP</th><th>Net unit<br>price</th><th>Qty</th><th>Total price<br>(KWD)</th></tr></thead><tbody>
            """);
        var sequence = 0;
        foreach (var line in order.Lines)
        {
            var p = line.Pricing;
            var discount = p?.DiscountType == "Percentage" ? $"{p.DiscountValue.ToString("0.###", CultureInfo.InvariantCulture)}%" : p?.DiscountType == "Fixed" ? $"{M(p.DiscountValue)} KWD" : null;
            var priceNote = discount is null ? "" : $"<small class='price-note'>Gross {M(p?.SupplierPrice)}<br>−{E(discount)}</small>";
            html.Append($"<tr><td>{++sequence}</td><td>{E(line.Sku)}</td><td class='barcode'>{E(p?.Barcode)}</td><td>{E(p?.Brand)}</td><td class='product-name'>{E(line.Name)}</td><td class='num'>{M(p?.Rsp)}</td><td class='num'>{M(p?.NetUnitPrice)}{priceNote}</td><td class='num'>{line.Quantity.ToString("0", CultureInfo.InvariantCulture)}</td><td class='num'>{M(p?.LineTotal)}</td></tr>");
        }
        html.Append($"</tbody></table><section class='summary'><div class='quantity-summary'>{order.Lines.Length} item lines<br>Total quantity: {order.Summary.TotalQuantity.ToString("N0", CultureInfo.InvariantCulture)}</div><div class='totals'>");
        if (order.Summary.UnpricedLines == 0)
            html.Append($"<p><span>Gross total</span><span>{M(order.Summary.GrossAmount)}</span></p><p><span>Total discount</span><span>{M(order.Summary.DiscountAmount)}</span></p><p class='final'><span>Total (KWD)</span><span>{M(order.Summary.NetAmount)}</span></p>");
        else
            html.Append($"<p><span>Priced subtotal (KWD)</span><span>{M(order.Summary.NetAmount)}</span></p><div class='partial'>{order.Summary.UnpricedLines} unpriced line(s). A complete order total is not available.</div>");
        html.Append("</div></section>");
        if (!string.IsNullOrWhiteSpace(order.Remarks)) html.Append($"<section class='remarks'><h2>Remarks</h2><p>{E(order.Remarks)}</p></section>");
        var footer = string.IsNullOrWhiteSpace(o.FooterAssetPath) ? E(o.CompanyName) : CustomAsset(o.FooterAssetPath, "footer-artwork", o.CompanyName);
        html.Append($$"""
            <footer class="document-footer"><span>{{footer}}</span><span>{{E(order.Number)}} · Revision {{order.Revision}}</span></footer></main>
            <script>Promise.all([document.fonts.ready,...Array.from(document.images).map(image=>image.complete?Promise.resolve():new Promise(resolve=>{image.onload=resolve;image.onerror=resolve;}))]).then(()=>{const button=document.getElementById('print-button');button.disabled=false;button.textContent='Print / Save as PDF';});</script></body></html>
            """);
        return html.ToString();
    }

    private string CustomAsset(string path, string cssClass, string alt)
    {
        var root = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "PrintAssets")) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(root, path));
        if (!full.StartsWith(root, StringComparison.Ordinal)) throw InvalidBranding();
        return Image(full, cssClass, alt);
    }
    private static string Image(string full, string cssClass, string alt)
    {
        var extension = Path.GetExtension(full).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg") || !File.Exists(full) || new FileInfo(full).Length > 1048576) throw InvalidBranding();
        return $"<img class='{cssClass}' alt='{E(alt)}' src='data:image/{(extension == ".png" ? "png" : "jpeg")};base64,{Convert.ToBase64String(File.ReadAllBytes(full))}'>";
    }
    private static Wros.Api.Shared.Errors.ApiProblemException InvalidBranding() => new(503, "PO print branding asset is missing or invalid. Include BrandingAssets with the API build; custom PNG/JPEG assets under PrintAssets must be at most 1 MB.");
}
