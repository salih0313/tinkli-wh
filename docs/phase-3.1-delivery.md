# Phase 3.1 delivery

Supplier availability files now feed a supplier-specific catalog and compact PO preparation workspace. Import does not access receiving, stock lots, balances or movement-writing services. POs retain numbering, send/resend revision history and the receiving lock; no emails or Delivery Notes are created automatically.

## Database changes

Additive migration `20261019062315_Phase31SupplierCatalogPricing` adds:

- `SupplierProducts`: stable unique `(SupplierId, Code)`, optional explicit Product FK, decimal supplier/discount/net pricing, reference RSP and availability, latest import FK, active flag, actor/timestamps and row version.
- `SupplierStockImports`: supplier FK, SHA-256, original filename, header/mapping metadata, timestamp/actor, confirmation state and row version. A non-unique `(SupplierId, FileHash)` index retains traceability while allowing repeated supplier lists. Supplier + product code identifies catalog entries; repeat confirmation of one import ID returns the original result.
- `SupplierStockImportRows`: original bounded cell data, row number, explicit catalog FK and frozen confirmation result; unique `(ImportId, RowNumber)`.
- Nullable `PurchaseOrderLines.SupplierProductId` FK and `PricingJson`. The latter stores the saved full per-line pricing/identity snapshot with quantity and totals; existing revision JSON also includes that snapshot. Historical rows remain untouched with NULL pricing.

All earlier migration files are preserved. Applying the migration and owner SQL verification remain pending. No seed change is needed.

## API surface

All catalog endpoints require the existing `purchaseorders.manage` permission; mutations use the existing session-bound CSRF token. No role grants were changed.

Under `/api/v1/suppliers/{supplierId}/catalog`:

| Method / suffix | Purpose |
| --- | --- |
| GET `/` | Paged search (`page`, `pageSize`, `search`, optional `importId`) |
| POST `/` | Create manually entered supplier product and optional explicit master-product mapping |
| PUT `/{id}` | Update catalog data/pricing/mapping with row version; code is immutable |
| GET `/template` | Constant, safe `.xlsx` template download |
| POST `/imports` | Bounded multipart `file` upload, automatic mapping and staged preview; rate limited |
| GET `/imports` | Paged supplier-wide import history |
| GET `/imports/{id}` | Uploader-owned preview |
| PUT `/imports/{id}/mapping` | Validate selected field-to-column indexes and regenerate preview |
| POST `/imports/{id}/confirm` | Transactional, versioned, idempotent confirmation; rate limited |

Import fields use `Code`, `Name`, `Brand`, `Barcode`, `Rsp`, `SupplierPrice`, `DiscountType`, `DiscountValue`, `AvailableQuantity`, `SupplierCode`, `SupplierName`. Mapping values are zero-based column indexes. Unmapped irrelevant columns are ignored.

Existing PO create/update endpoints now accept `supplierProductId`, `originalLineId`, `refreshPrice`, `catalogRowVersion` per line, alongside ProductId/Quantity. New lines use mapped catalog entries; existing saved lines preserve prices unless explicitly refreshed. Backend ignores submitted client totals and derives decimal snapshots. Read endpoints include pricing snapshots and summary (with exact monetary text for large values); historical line prices are nullable.

`POST /api/v1/purchase-orders/{id}/print-document` accepts the existing VersionRequest and returns `{html}` for a consistent saved PO revision, auditing the operation in the same transaction as the versioned read. Existing `prepare-print` remains available. Both require purchasing permission. HTML encodes user-provided labels/remarks; no supplier data becomes executable markup. Browser Print / Save PDF provides the file.

Spreadsheet handling follows the package/worksheet/shared-string structure documented by [Microsoft SpreadsheetML documentation](https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/structure-of-a-spreadsheetml-document). The intentionally limited reader uses built-in ZIP/XML APIs; no additional runtime package or Excel automation process was added.

## Frontend and files

Updated screens: PurchaseOrderEditor and PurchaseOrderDetails. New reusable purchasing components: ExcelImportDialog, CatalogPicker, SupplierProductDialog and PricingSummary, with typed catalog/import/pricing contracts. The editor shows current-import versus all-preparation views, local search, read-only product/pricing labels, quantity-only editing, explicit Common Quantity application, duplicate-line prevention and errors that retain work. Responsive cards replace the wide working table on phones. Catalog/history/preview tables scroll inside dialogs. Save first, then open the dedicated printable document from the detail screen.

The router now uses React Router's data-router provider with the same route tree/session guards to support a proper navigation blocker (including browser Back). Browser close/reload also warns on unsaved changes. No loader/API/session architecture was replaced.

Created:

- `src/Wros.Api/Controllers/SupplierCatalogController.cs`
- `src/Wros.Api/Modules/Purchasing/SupplierCatalog/Models.cs`
- `src/Wros.Api/Modules/Purchasing/SupplierCatalog/XlsxFile.cs`
- `src/Wros.Api/Modules/Purchasing/SupplierCatalog/SupplierCatalogService.cs`
- `src/Wros.Api/Modules/Purchasing/PurchaseOrderPrintService.cs`
- `src/Wros.Api/Shared/Persistence/Migrations/20261019062315_Phase31SupplierCatalogPricing.cs` and `.Designer.cs`
- `src/wros-web/src/features/purchasing/catalog.ts`
- `src/wros-web/src/features/purchasing/ExcelImportDialog.tsx`
- `src/wros-web/src/features/purchasing/CatalogDialogs.tsx`
- `src/wros-web/src/features/purchasing/PricingSummary.tsx`
- `docs/phase-3.1-manual-checks.md`, `docs/phase-3.1-delivery.md`

Modified:

- API Purchasing `Entities.cs`, `Contracts.cs`, `PurchasingConfiguration.cs`, `PurchaseOrderService.cs`
- `Controllers/PurchaseOrdersController.cs`, `Program.cs`, `Shared/Persistence/WrosDbContext.cs`, EF model snapshot
- `appsettings.json`, `appsettings.example.json`
- Frontend `App.tsx`, `api/client.ts`, purchasing `types.ts`, `pages/PurchaseOrderEditor.tsx`, `pages/PurchaseOrderDetails.tsx`, `styles.css`
- `README.md`

## Verification and handover

Run commands, configuration defaults, decisions, limitations, all **24 numbered manual checks with expected results**, and read-only SQL are in [the manual acceptance guide](phase-3.1-manual-checks.md). Verification completed:

| Check | Result |
| --- | --- |
| Backend Debug compilation (EF scaffolding helper) | Passed |
| `dotnet build Wros.slnx -c Release --no-restore` | Passed, zero warnings/errors |
| `npm --prefix src/wros-web run build` | Passed (TypeScript + Vite production build) |
| EF `migrations has-pending-model-changes` | Passed: model matches checked-in migration |
| EF idempotent migration script generation | Passed; SQL was not executed |
| `git diff --check` | Passed |

The isolated temporary scaffold helper reported NU1801 warnings because the restricted network could not fetch NuGet repository-signature information. Its cached dependencies were sufficient to scaffold and inspect the migration; no helper project/dependency was added to WROS. The normal solution Release build has zero warnings/errors.

 No automated tests, framework or CI pipeline was added. Runtime/SQL migration, browser responsive behavior and multi-page PDF output require owner testing.

Limits: first worksheet/values-only XLSX, KWD/whole units, up to 500 PO lines, explicit master-product creation/mapping, browser-based rather than binary server-side PDF rendering, retained staging/history without an automatic purge policy, and no automatic recovery of unsaved work after a browser/process crash. Review the documented three-decimal unit-first midpoint-away-from-zero rounding rule and historical unpriced presentation during acceptance.

**Please run the manual checks and explicitly approve Phase 3.1 before any further phase.**
