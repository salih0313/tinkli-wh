# Supplier import: optional prices and sequence numbers

Only code and name are required for imported and manually saved supplier catalog products. Other spreadsheet fields may be blank. Supplied numbers must still be valid; prices use up to three decimal places and quantities are non-negative whole units. Missing prices persist as NULL, distinct from an explicit zero. Missing supplier quantity remains NULL. Required PO quantities are entered by the user before saving.

## Files changed

- API purchasing: SupplierCatalog/Models.cs, SupplierCatalog/SupplierCatalogService.cs, PurchaseOrderService.cs, PurchaseOrderPrintService.cs.
- Web: features/purchasing/{catalog.ts,CatalogDialogs.tsx,ExcelImportDialog.tsx,PricingSummary.tsx}, pages/PurchaseOrderEditor.tsx.
- EF migration OptionalSupplierCatalogPrices, its designer, and WrosDbContextModelSnapshot.cs.
- This manual guide.

## Setup and startup

Use the existing SQL Server connection and secrets described in local-development.md. No new configuration or seed data is required. Apply the migration before running the updated application:

```sh
dotnet ef database update --project src/Wros.Api
dotnet run --project src/Wros.Api --launch-profile https
```

In another terminal:

```sh
cd src/wros-web
npm run dev
```

## Owner verification

1. Upload the original wros-supplier-stock-filled.xlsx with C2, C3, F2 and F3 blank: expect 5 valid rows, no invalid rows, blank prices displayed as a dash, and sequence 1–5.
2. Upload a workbook containing only code and name columns: expect valid rows without requiring price or quantity mappings. Confirm: catalog prices and supplier quantities stay blank, including after reopening.
3. Remove code or name from a row: expect validation and confirmation disabled. Enter malformed, negative or over-precision prices, or fractional quantities: expect validation rather than silent conversion.
4. Edit an imported supplier product, enter a price and supplier quantity, and save: expect both values retained. Clear the price and save: expect a blank price rather than 0.000. Explicitly enter zero: expect 0.000.
5. Map an unpriced catalog entry to a WROS product, add it to a PO, and enter a required quantity. Save/reopen and print: expect quantity retained, blank unit price and line total, and an unpriced-line notice. Priced lines retain normal calculations; totals exclude unpriced lines.
6. Edit the catalog price and explicitly refresh the PO price using the existing workflow: expect the new price and totals. Existing saved PO prices remain unchanged until refreshed.
7. Preview more than 20 entries with a blank spreadsheet row between entries: expect sequence 1–20 then 21 onward, with original Excel row numbers available on hover for troubleshooting.

## Limitations

Brand is optional in imports and manual catalog forms; no additional migration or configuration is needed for optional brand. This change does not infer brands or prices, create product mappings automatically, or change warehouse stock. Blank optional cells replace those catalog values when confirming an update, consistent with the existing import replacement behavior. Existing zero prices remain zero; their original intent cannot be inferred. Live SQL migration and browser acceptance require owner verification. No automated tests were added.
