# Supplier-facing purchase order print / PDF

The print document follows the supplied Tinkli reference: full logo, company contacts, purchase-order title and reference/date box, purple TO/SHIP TO bars, numbered item grid, totals, remarks and a purple footer rule. It uses A4 portrait with millimetre margins and embedded image data, independent of application UI styles or external image URLs.

The supplier name, contact person, phone, email and address come from the selected supplier. Code/name/brand/barcode and all financial values come from the saved PO pricing snapshot. Net unit price is labelled accurately; any percentage/fixed discount is shown with the supplier gross price in that cell, never inferred from RSP. Blank brands/barcodes/prices display a dash. Unpriced orders show a priced subtotal and an explicit incomplete-total message. The sample reference's consignment wording and payment terms are not copied automatically.

## Changed files

- src/Wros.Api/Modules/Purchasing/PurchaseOrderPrintService.cs: print layout, embedded logo, configurable contacts/terms, pagination and optional-price presentation.
- src/Wros.Api/Modules/Purchasing/{Contracts.cs,PurchaseOrderService.cs}: expose the existing supplier contact details to the print renderer.
- src/Wros.Api/{appsettings.json,appsettings.example.json}: non-secret print configuration, with reference company/ship-to contacts as editable defaults.
- src/wros-web/src/pages/PurchaseOrderDetails.tsx: supplier PDF/email workflow feedback.
- This guide.

## Run

No migration or seeding is required. Restart the updated API and reload the frontend:

```sh
dotnet run --project src/Wros.Api --launch-profile https
```

From src/wros-web, use `npm run dev` for local work or `npm run build` for deployed assets. Backend build: `dotnet build src/Wros.Api --no-restore`.

## Print configuration

Review PurchaseOrderPrint in appsettings.json. Use appsettings.Development.json for non-secret local overrides or the deployment's configuration mechanism. All added fields are optional except the existing A4/margin validation; no secret values are needed.

- CompanyName, CompanyAddress, CompanyPhone, CompanyEmail, CompanyWebsite: letterhead details. Defaults use the supplied reference.
- ShipToName, ShipToAddress, ShipToContact: default recipient details from the reference. A missing ship-to name/address falls back to company name/address. These are print defaults; the PO still does not select a receiving warehouse.
- PaymentTerms and DeliveryMethod: omitted unless explicitly configured. Configure only terms appropriate for the orders being printed; they are not derived from supplier remarks.
- RequestMessage: defaults to a neutral supply request. Change only if the intended wording applies to these orders; no consignment agreement is assumed.
- HeaderAssetPath and FooterAssetPath: existing optional custom PNG/JPEG assets under PrintAssets, maximum 1 MiB each. Leave null to use the supplied Tinkli logo and standard footer. Standard logo is packaged from the existing web branding source with API build/publish output.

Example local non-secret override:

```json
{
  "PurchaseOrderPrint": {
    "ShipToName": "TINKLI KIDS PLANET",
    "ShipToAddress": "Building No 8, Floor 1, Office 6\nShaker Al Kazemi Building\nBlock 6, Sharq, Kuwait.",
    "ShipToContact": "+965 50954522",
    "PaymentTerms": null,
    "DeliveryMethod": null
  }
}
```

## Owner acceptance

1. Review the configured company address/phones/email and default ship-to against the intended destination. Complete the supplier's contact details in Suppliers. Open a saved PO and choose Print / Save PDF: expect the branded supplier document, correct reference, dates, contacts and source order quantities.
2. Choose Print / Save as PDF in the preview. Select Save as PDF (Chrome) or Save as PDF from the system PDF menu (Safari), A4 portrait, and disable browser headers/footers. Expect the document title/reference to identify the saved file. Print controls and app toasts must not appear in the PDF. Use the PDF as an email attachment; the app does not send supplier email.
3. Compare every code, barcode, brand, quantity, unit price, discount, line total and total with the saved PO. Confirm leading zeros remain intact, three-decimal KWD values are retained, and RSP is not used as the discount base. Price updates in the catalog must not silently alter the saved document.
4. Print a short four-line order. Expect it fits one page with totals, remarks and the footer. Print a 45-line order with long product names: expect all rows, repeated item headings, complete final totals and normal page breaks without rows split or clipped. Inspect the final footer and final page for blank pages.
5. Print an order with blank brand/barcode/prices and special characters such as ampersands/quotes in names and remarks. Expect dashes for missing optional values, safe literal text, readable wrapping and an incomplete-total note instead of a misleading complete total.
6. Save the PDF, close the preview, reopen the downloaded file and view it offline. Expect the logo and document remain intact. Confirm the print button waits for image/font readiness before enabling.
7. Try a custom header/footer if used in deployment. Expect the artwork is embedded and remains within the document width. Missing/out-of-bounds assets must return a structured error toast. Print access remains permission checked, concurrency checked and audited.

## Validation and limits

Backend/frontend builds and whitespace checks pass. Manual render previews use the actual .NET print renderer and saved-DTO-shaped sample data; short, multi-page and unpriced PDFs were reviewed in Chrome: the four-line and unpriced samples each fit one page; the 45-line sample spans four pages with repeated item headers, final totals and no footer-only extra page. Scratch preview generation is outside the repository; no automated tests/frameworks were added. The owner still needs to verify real supplier/order values and their browser's print settings.

PDF saving uses the browser/system print dialog; a direct server-generated PDF download endpoint is not added. Browser/printer pagination can vary. Contact/term defaults are current print configuration, not immutable per-order contact/term snapshots; the saved item pricing remains immutable until explicitly refreshed. The footer follows the final content rather than being forced onto every page, avoiding overlap or a footer-only extra page. Approve the visual layout and PDF checks before further changes.
