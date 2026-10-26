# Phase 3.1 — Supplier stock import and PO preparation

Implementation/build verification is separate from acceptance. **The owner must run these checks; manual testing has not been claimed as passed.** Stop after this phase and obtain owner approval before further scope.

## Setup and commands (macOS / Windows PowerShell)

Run from the repository root. Keep the existing SQL Server database, migrations, User Secrets, staff permissions and Development seed setup. This change does not reset data or add seed users/sample transactions.

```sh
dotnet restore Wros.slnx --locked-mode
dotnet tool restore
dotnet build Wros.slnx --no-restore
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet run --project src/Wros.Api --launch-profile https
```

In another terminal:

```sh
npm --prefix src/wros-web ci
npm --prefix src/wros-web run dev
```

Open http://localhost:5173. Existing Vite proxy targets https://localhost:7243. Check `/api/v1/health/ready` and development `/openapi/v1.json` through the proxy. Use your existing accounts. If initial account setup is needed, follow [local development](local-development.md) and the existing ignored Development credential file flow; no new seed command/credentials are introduced in this phase.

For a new database connection, use a locally supplied SQL Server connection through User Secrets, never tracked JSON. Replace the placeholder locally, and do not paste actual credentials into reports:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your SQL Server connection>" --project src/Wros.Api
```

The new additive migration is `Phase31SupplierCatalogPricing`. Do not remove older migrations or recreate the database. For future migrations (not required to apply this delivery):

```sh
dotnet ef migrations add YourNextChange --project src/Wros.Api --startup-project src/Wros.Api --output-dir Shared/Persistence/Migrations
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
```

The usual EF host-discovery path stalled in the restricted agent environment. The checked-in migration was scaffolded using an isolated temporary design-time host and a dummy SQL connection; scaffolding does not contact SQL. That temporary helper is not a project dependency or required to run WROS. Database application and SQL-backed runtime checks are for the owner environment.

## Configuration and decisions

Non-secret defaults are included in `src/Wros.Api/appsettings.json` and `appsettings.example.json`:

| Key | Default / behavior |
| --- | --- |
| `SupplierImport:MaxFileBytes` | 5242880 (5 MiB); configurable up to 10 MiB |
| `SupplierImport:MaxExpandedBytes` | 33554432 (32 MiB); ZIP expansion / XML processing bound |
| `SupplierImport:MaxRows` | 2000 data rows; configurable up to 5000 |
| `SupplierImport:MaxColumns` | 64; configurable up to 128 |
| `SupplierImport:AllowedExtensions` | `[".xlsx"]`; other formats are not implemented |
| `SupplierImport:DefaultCurrency` | `KWD`; three-decimal currency only |
| `SupplierImport:PreviewHours` | 24; unconfirmed previews expire |
| `PurchaseOrderPrint:PageSize` | `A4` |
| `PurchaseOrderPrint:MarginMillimeters` | 12; range 8–25 |
| `PurchaseOrderPrint:HeaderAssetPath` | null; neutral WROS placeholder |
| `PurchaseOrderPrint:FooterAssetPath` | null; neutral WROS placeholder |

Optional print assets are relative PNG/JPEG paths **inside `src/Wros.Api/PrintAssets`**, each at most 1 MiB. For example, place `official-header.png` there and set `PurchaseOrderPrint:HeaderAssetPath` to `official-header.png`. No company identity, address or legal text has been invented. Images are embedded into the print document; no external asset service is required. Configuration is validated on startup; configured asset existence/type is checked when rendering.

Rules chosen explicitly for this phase:

- Supplier-specific codes are trimmed and uppercased, unique per supplier, and immutable after creation. Names/barcodes never merge products or select a WROS identity.
- Code, name and supplier purchase price are required; brand, barcode, RSP and availability are optional. Product master expiry settings remain in the existing Product workflow.
- Import uses the **first worksheet**, with its first non-empty row as headers. Use values only. Mapped formula/error cells, macro content, external links/hyperlinks, malformed XLSX and oversized workbooks are rejected. Unmapped extra columns (including boolean/date/formula cells) are ignored without evaluating formulas or consuming their cached values.
- Decimal point only; no currency symbols, thousands separators or ambiguous localized numbers. Set code/barcode cells to Text before entry to retain leading zeros; lost Excel numeric formatting cannot reconstruct product identity.
- Header aliases map fields only when unambiguous. Generic `Price` is deliberately not auto-mapped. A nonzero generic discount requires an explicit type; an explicitly labelled percentage/amount column or `%` value identifies the type. Percentage-formatted Excel cells are interpreted as percentages.
- Prices/RSP/discount values accept at most three decimal places. Supplier prices/RSP are bounded at 999999999 KWD. Negative prices and invalid discounts are rejected. Zero is allowed. Missing discount means zero.
- Backend uses C# decimal. Net unit price rounds to three decimals, midpoint away from zero, **before multiplying by whole-unit quantity**. Line discount is `(original price − rounded net unit price) × quantity`. Totals sum these saved line amounts. Frontend quantity previews multiply integer fils; exact text fields preserve large saved totals without JavaScript number rounding.
- All new PO lines require catalog pricing and an explicit WROS product link. Historical unpriced lines remain unpriced, are editable with their original IDs, and are reported separately; no migration fabricates old prices. New manual entries use a separate supplier-product dialog.
- A new canonical WROS product is created through the existing Products screen in another tab (requires `products.manage`). Reopen the mapping dialog to refresh choices. Purchasing users can explicitly map catalog records but cannot create master products through the import API.
- A PO retains its existing 500-line maximum. Larger imports are allowed as catalogs; remove unwanted preparation rows before saving. New rows start with quantity 1, not the current Common Quantity.
- Importing a new file selects its products as the working view. Previous rows/quantities already in preparation remain in **All PO products**; older unrelated catalog entries appear only through Add existing products. Re-adding a supplier product retains its existing row and quantity.
- Catalog updates never rewrite saved line pricing. Explicit Refresh catalog prices applies the latest pricing; requests carry row versions to reject unexpected catalog changes. Supplier catalog inactivity/remapping does not silently rewrite an existing snapshot.
- Confirmation is all-or-nothing. Identical supplier files may be uploaded and confirmed again as separate imports. Retrying the same confirmed import ID returns its result without applying it again. A revised workbook with different bytes can update the same stable supplier codes.
- Only the uploading user can preview/map/confirm their staged import. Purchasing-authorized users can view supplier-wide import history. Old preview records are retained; automatic cleanup and restoring unsaved work after browser/process crashes are outside this phase.
- Print/Save PDF uses a dedicated server-rendered HTML document and the browser's A4 PDF printer. Repeated table headers, wrapped names and a final totals block are included. Browser page headers/footers should be disabled in the print dialog. Official branding occupies the first header/final footer; repeating branding on every page is not implemented.

Owner confirmation is requested for the rounding rule, explicit manual master-product creation/mapping workflow and historical unpriced presentation. These are documented implementation choices; no existing stock, numbering, receiving lock or authorization assignments are changed.

## Manual acceptance checks

Prepare one active supplier, at least three active WROS products (using the existing Products screen), and an account with `purchaseorders.manage`. Use a second staff account without that permission for authorization checks. Record baseline inventory totals and existing PO/revision counts before applying the migration if you want a before/after comparison.

Use this small workbook A (copy the exact template headers; optional supplier code/name columns may be added):

| Supplier Product Code | Product Name | Brand | Barcode | RSP/MRP | Supplier Purchase Price | Discount Type | Discount Value | Supplier Available Quantity |
| --- | --- | --- | --- | ---: | ---: | --- | ---: | ---: |
| SUP-A | Percentage example | Example | 000001 | 15.000 | 10.000 | Percentage | 20 | 2 |
| SUP-B | Fixed example | Example | 000002 | 18.000 | 10.000 | Fixed | 1.500 | 50 |
| SUP-C | No discount example | Example | 000003 | 7.000 | 3.250 | None | 0 | |

Use text cells for codes/barcodes. Use literal numeric cells for amounts; never formulas.

1. **Template:** Create a PO, select the supplier, choose Import Excel → Download Excel template. Open it in Excel/LibreOffice. **Expected:** valid `.xlsx` with the nine requested headers and no supplier identity/credentials or formulas.
2. **Valid import:** Fill workbook A, upload, inspect mapping and preview, then confirm. **Expected:** three valid/new rows, net prices 8.000 / 8.500 / 3.250, explicit confirmation, three catalog records and an audited confirmed import. New records are unresolved until explicitly mapped. Map each through its Map action to the WROS products you prepared.
3. **Different columns:** Make workbook B with reordered headers `SKU`, `Item Name`, `Manufacturer`, `EAN`, `Retail Price`, `Purchase Price`, `Discount Type`, `Discount Value`, `SOH` plus an irrelevant column. Use new codes. **Expected:** recognized aliases auto-map, extra column ignored, manual correction and Validate mapping & preview work. A generic `Price` header remains unmapped until you select it. Missing code/name/price mapping blocks confirmation. Include your selected supplier code/name columns and then deliberately mismatch one; mismatch must block import even if the other identity field matches.
4. **Invalid data:** Upload copies with a blank code/name, negative price, 101% discount, fixed discount above price, nonnumeric/ambiguous amounts, nonzero discount without a type, duplicate codes with different case, and a formula. Also try `.xls`/`.csv`, renamed invalid bytes, oversize files, and workbooks with external links. **Expected:** row-level issues for invalid mapped values and mapped formula/error cells; unsafe/malformed files rejected at upload. Unmapped extra boolean/date/formula columns should not block otherwise valid rows. No catalog rows partially update. Resolve the file/mapping and upload/validate again. A duplicate code is never silently selected.
5. **Update prices:** Save a PO from workbook A first. Change SUP-A's price to 12.000 in another workbook and import it. **Expected:** same catalog ID and WROS mapping; latest net price 9.600; new import/history entries. No duplicate supplier code and no automatic PO repricing.
6. **Retain older products:** Import a workbook containing only SUP-A. **Expected:** SUP-B/SUP-C remain in the supplier catalog with their original active status and pricing. Nothing is automatically deactivated.
7. **Current import only:** On a fresh PO import the SUP-A-only file. **Expected:** primary list contains only this import. If work already existed, its rows and entered quantities remain accessible under All PO products, without silently mixing into the Current import view.
8. **Previous products:** Click Add existing products, search SUP-B, select it and add. Search by barcode as well. **Expected:** prior imports are searchable/paginated; added row joins the same PO, quantity 1; duplicates retain the existing quantity. Unresolved/inactive records cannot be selected until mapped/activated.
9. **Manual product:** Add manually with a unique supplier code, supplier price and discount; select an existing WROS product explicitly. Also create a brand-new master product through the linked Products tab and reopen the dialog to map it. **Expected:** separate dialog validates details/prices, creates/updates only supplier catalog/master data as explicitly requested, then adds a read-only priced row. A second supplier using the same code/name/barcode does not merge catalogs or infer the same Product ID.
10. **Common Quantity:** Enter 5 and click Apply to all. Then change the Common Quantity input to 7 without clicking Apply. **Expected:** all current PO rows become 5 only on Apply; remain 5 when just editing the common input. Apply includes rows hidden by filters. A newly added product starts at 1 and existing quantities remain unchanged.
11. **Individual quantity:** Set SUP-A to 6, clear/re-enter it, and try fractional, negative and excessive quantities. **Expected:** previews update immediately; save accepts positive safe whole units and rejects invalid values. Ordering 6 when supplier quantity is 2 is allowed. Grid details/prices are labels; quantity is the only editable data cell.
12. **Remove/filter/unsaved work:** Search and remove a product; verify hidden rows are retained. Follow Cancel, browser Back and another sidebar link after changing quantities; test browser reload/close warning. Choose Keep editing. Simulate a save API failure using browser network tools. **Expected:** catalog product remains; navigation warns; cancelling navigation and failed saves retain quantities. No automatic cross-session recovery is claimed.
13. **Percentage calculation:** With SUP-A at supplier price 10.000 and 20% discount, quantity 5, save. **Expected:** net unit 8.000, line gross 50.000, line discount 10.000, line net 40.000. RSP 15.000 never enters calculations. Also use a raw numeric percent cell formatted as 20%; verify import shows 20%, not 0.2%.
14. **Fixed calculation:** With SUP-B at 10.000, fixed 1.500 and quantity 5, save. **Expected:** net unit 8.500, gross 50.000, discount 7.500 and net 42.500. No second discount application. Check 0%/100%, zero price/no discount and exact fixed-price discount boundaries.
15. **Summary and rounding:** Keep only SUP-A and SUP-B, both quantity 5. **Expected:** 2 products / quantity 10 / gross 100.000 / discount 17.500 / net 82.500 KWD in editor, saved response and detail view. For price 0.005 and 50%, net unit rounds to 0.003; quantity 5 gives 0.015, line discount 0.010. Client-supplied totals/net prices are ignored; backend derives them from catalog/saved snapshots.
16. **Save/reopen/revisions:** Save the priced PO, reopen and edit only quantity/remarks, then save, record send and resend. **Expected:** all codes/labels/prices/discounts persist from snapshots; numbering/status/revisions behave as before. Open a pre-migration unpriced PO: quantities and revisions remain, missing prices show “—”, totals explicitly exclude unpriced lines, no price backfill occurs.
17. **Historical price protection:** Update the supplier catalog again, reload the saved PO and edit quantities without refreshing. **Expected:** saved price/metadata remain. Choose Refresh catalog prices explicitly and save an editable PO; latest values apply and a new revision preserves the previous snapshot. Change a catalog row in another tab after selecting it for a new PO, then save: **Expected:** 409 stale-catalog error and preparation retained; refresh latest catalog prices before retrying.
18. **Receiving lock:** Create a draft Delivery Note from a saved PO using the existing workflow. Attempt PO edit, price refresh via save and resend. **Expected:** existing lock stays enforced by the API (409); no unlock behavior or automatic DN creation was added.
19. **Multi-page PDF:** Prepare 80+ mapped lines with long names/brands/barcodes and varied quantities; save and click Print / Save PDF. Allow the print window, choose A4 portrait, normal scale, browser headers/footers off, save PDF. **Expected:** wrapped text, aligned amounts, repeated column headers on each page, complete lines, no clipped totals. Repeat with configured official header/footer images if available; absent assets use neutral WROS placeholders.
20. **PDF amount equality:** Compare PDF line amounts and gross/discount/net with `GET /api/v1/purchase-orders/{id}` and saved snapshots. **Expected:** exact three-decimal equality, including rounding example; historical unpriced POs never show a fabricated complete financial total. Update the PO in another tab before opening print with a stale version: **Expected:** conflict instead of printing an unnoticed revision.
21. **Inventory isolation:** Run the inventory SQL below before and after upload, confirmation, mapping, PO save and PDF generation (with no other warehouse operations running). **Expected:** identical balances, ledger count and quantities; no lots, DN or inventory movements generated by import. Existing receipt posting is still the stock-in mechanism.
22. **Permissions and audit:** Without `purchaseorders.manage`, request catalog search/template/history and POST/PUT import/catalog/PO endpoints using the current session/CSRF flow. With permission, try supplier A's catalog ID under supplier B, forged OriginalLineId, unmapped Product ID, duplicate supplier products and stale row versions. Use another purchasing user to access a staged preview. **Expected:** denied/validation/conflict responses with correlation IDs; no data mutation. Products creation still requires `products.manage`. Audit Viewer records upload/confirmation, catalog mapping/pricing changes, PO revisions and printing with the validated actor; no passwords/tokens/file bodies logged.
23. **Responsive/accessibility:** At desktop, tablet (768px) and mobile (390px), use keyboard and touch to map/import, enter quantities, apply common quantity, remove rows and save. **Expected:** compact desktop table with local scroll/sticky header; mobile product cards, readable values, usable quantity controls, scrollable contained dialogs, visible actions and no page-level overflow. Check focus/Tab/Escape and form labels.
24. **Duplicate protection/history:** Click confirm twice or replay the same import ID/rowVersion after success. Upload identical bytes again for the same supplier; upload them for a different supplier without embedded identity columns. Import a revised workbook with the same product codes. **Expected:** same-ID retry makes no additional catalog/audit updates; a fresh upload of identical bytes can be confirmed, updating the same supplier codes without duplicating catalog entries; different suppliers remain isolated; revised files update stable records. History has UTC timestamps, actor, file, row count and status. Concurrent confirmation either returns the original result or conflicts and safely succeeds on retry, never partial/duplicate updates. A timeout after commit can be safely retried with the same ID.

## Read-only SQL verification

Run in your existing WROS database; no schema/data mutation scripts are needed. EF migration history remains intact.

```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;

-- Stable supplier identity; expect zero rows.
SELECT SupplierId, Code, COUNT(*) AS Duplicates
FROM SupplierProducts GROUP BY SupplierId, Code HAVING COUNT(*) > 1;

SELECT Id, SupplierId, Code, ProductId, SupplierPrice, DiscountType,
       DiscountValue, NetUnitPrice, LatestImportId, IsActive, UpdatedAt
FROM SupplierProducts ORDER BY SupplierId, Code;

SELECT Id, SupplierId, FileName, CreatedAt, CreatedBy, ConfirmedAt
FROM SupplierStockImports ORDER BY CreatedAt DESC;
SELECT ImportId, RowNumber, SupplierProductId, ResultJson
FROM SupplierStockImportRows ORDER BY ImportId, RowNumber;

-- Saved PO values; old lines should retain NULL PricingJson.
SELECT o.Number, o.Revision, l.Id, l.SupplierProductId, l.Quantity,
       JSON_VALUE(l.PricingJson, '$.SupplierProductCode') AS SupplierCode,
       JSON_VALUE(l.PricingJson, '$.SupplierPrice') AS OriginalPrice,
       JSON_VALUE(l.PricingJson, '$.DiscountType') AS DiscountType,
       JSON_VALUE(l.PricingJson, '$.NetUnitPrice') AS NetUnitPrice,
       JSON_VALUE(l.PricingJson, '$.LineTotal') AS LineTotal
FROM PurchaseOrders o JOIN PurchaseOrderLines l ON l.PurchaseOrderId=o.Id
ORDER BY o.Number, l.Position;

-- Baselines must stay identical during isolated supplier import/PO actions.
SELECT COUNT_BIG(*) AS BalanceRows, SUM(Quantity) AS PhysicalQuantity FROM InventoryBalances;
SELECT COUNT_BIG(*) AS MovementRows, SUM(Quantity) AS LedgerQuantity FROM StockMovements;
SELECT COUNT_BIG(*) AS Lots FROM StockLots;
SELECT COUNT_BIG(*) AS DeliveryNotes FROM DeliveryNotes;

SELECT Timestamp, ActorId, Action, Entity, EntityId, CorrelationId
FROM AuditLogs
WHERE Entity IN ('SupplierProduct', 'SupplierStockImport', 'PurchaseOrder')
ORDER BY Timestamp DESC;
```

Owner approval gate: record actual results, report any discrepancies, and approve Phase 3.1 explicitly before another phase begins.
