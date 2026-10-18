# Phases 2–4 delivery guide

This cycle implements Phases 2, 3 and 4 sequentially, as explicitly authorized by the owner. Phases 0/1/1.5, existing authentication and the professional reusable UI remain in place. Phase 5 has not started. No automated tests, competing UI framework or external services were introduced.

## Delivered by phase

| Phase | Backend and data | Frontend |
| --- | --- | --- |
| 2 | Supplier/product/warehouse/area/rack/optional-location records, scoped uniqueness, row-version conflict handling, active status, reference read policy, permission-checked maintenance, safe audits | Master lists, search/status filters, hierarchy navigation, create/edit/view dialogs; shared responsive table, form and lookup components |
| 3 | Transactional yearly PO counters; whole-unit draft/sent/locked orders; edit/send/resend; immutable revision snapshots; print-preparation audit | PO list/filter, create/edit, details, revisions, print layout and browser Save as PDF |
| 4 | Standalone/from-PO draft DNs; lots and expiry; split placements; atomic post; original receipt allocations, separate current assignments, physical balances and append-only signed ledger; safe in-warehouse rack corrections; occupied-subtree protection | DN list/create/edit/details; PO-copy and product creation via existing master screen; lot/allocation editors, posting confirmation, correction editor, receipt ledger and basic read-only receipt stock |

## Owner decisions and scope boundaries

- Quantities are **whole units**, stored using decimal(18,0); no fractional UOMs, variants, prices, currencies or tax rules are invented. Safe JSON integer bounds are validated on both sides.
- Numbers use **PO-YYYY-NNNNNN / DN-YYYY-NNNNNN**, with a separate transactional counter per type and UTC creation year. A backdated PO/received timestamp does not change an already allocated number.
- **Send/resend records state and revision for manual supplier sharing.** There is no configured email/channel, so the system never claims to transmit a message. Print/download uses native browser print/Save as PDF, as approved.
- A draft DN locks its PO immediately and permanently for this version; multiple DNs can reference it. No cancel/delete/unlock feature is introduced while cancellation exceptions remain undecided.
- Racks can receive stock directly, with optional bin/drawer/tray positions. A product and its receipt lots may occupy many positions and warehouses.
- Posted rack corrections preserve each warehouse's existing quantity. Interwarehouse transfers, returns, disposal, stock-out and e-commerce remain deferred.
- Only a basic receipt stock view is provided here. Broad inventory search, FIFO/FEFO, expiry alerts and reporting remain Phase 5 under AGENTS.md.
- Optional external product ID is a stable reference only; the future mapping contract and variant relationships are not assumed or integrated.
- Existing master records use deactivation instead of physical deletion. Hierarchy parentage is immutable. Occupied locations and all their ancestors cannot be deactivated.
- Draft receipt rewriting may replace internal draft lot IDs. Once posted, lot identity/receipt data is stable; original allocations and ledger rows are preserved. Current nominal assignments are separate from current physical balances.
- Positive tracked lots require expiry at posting; expiry earlier than the UTC date and inactive products yield zero sellable quantity, with physical on-hand retained.

## Applied migrations

In order, preserving all existing migrations and data:

1. `20261017171601_Phase2MasterData`
2. `20261017172926_Phase3Purchasing`
3. `20261017174710_Phase4Receiving`

No reference/demo master records or transactions/balances were seeded. `warehouses.manage` is the only new permission definition, inserted idempotently and granted to SuperAdmin. No existing staff mapping is silently replaced. SuperAdmin retains full access. Use the existing Roles screen to assign suppliers.manage, products.manage, warehouses.manage, purchaseorders.manage, receiving.manage and inventory.view deliberately.

Master reads accept any of these six operational permissions. PO reference reads also accept receiving.manage to support copying a PO; PO writes/send/print preparation require purchaseorders.manage. DN actions/history require receiving.manage. Receipt stock requires inventory.view. Existing CSRF, secure cookies, lockout, session guards, validation/ProblemDetails, correlation IDs and user/role administration restrictions are reused. No frontend-supplied actor ID is used.

## Software, configuration and commands

Requirements remain .NET SDK 10, EF Core CLI 10.0.12 from the tool manifest, SQL Server (the existing local Docker container works on Mac), Node >=22.12, npm and a modern browser. No new NuGet/npm package or secret/config key was added. Reuse existing appsettings/User Secrets connection configuration and private Development account credentials. Do not put passwords in tracked files.

From repository root:

```sh
dotnet restore Wros.slnx
dotnet tool restore
dotnet build Wros.slnx
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet ef migrations list --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet run --project src/Wros.Api --launch-profile https
```

Separate terminal, also from root:

```sh
npm ci --prefix src/wros-web
npm run dev --prefix src/wros-web
```

Open http://localhost:5173. API https://localhost:7243. Production build: `npm run build --prefix src/wros-web`.

The existing Vite same-origin proxy handles authenticated API requests. Browser PDF output does not require a PDF service. SQL Server must be running before migrations/receiving. Do not use EnsureCreated, recreate the database or apply ad-hoc schema scripts. A future migration is generated with `dotnet ef migrations add Name --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development`.

## API inventory

All paths start `/api/v1`; use Development OpenAPI for exact schemas. All mutations use existing CSRF and version-sensitive writes require the last returned rowVersion.

| Resource | Methods/actions |
| --- | --- |
| suppliers, products, warehouses, areas, racks, locations | GET paged/search/status/parent, GET by Guid, POST, PUT by Guid |
| purchase-orders | GET paged/search/status; GET /{id}; GET /{id}/revisions; POST; PUT /{id}; POST /{id}/send; POST /{id}/prepare-print |
| delivery-notes | GET paged/search/status; GET /{id}; POST; PUT /{id}; POST /{id}/post; POST /{id}/correct-allocations |
| delivery-notes/purchase-orders | GET paged lightweight PO reference lookup |
| delivery-notes/placements | GET paged active rack/optional-position lookup |
| delivery-notes/{id}/movements | GET paged immutable receipt/relocation history |
| receipt-stock | GET paged basic physical receipt balances, optional deliveryNoteId |

There are no hard-delete, arbitrary stock-edit, reset-database, test consumption or external-message endpoints.

## Created/modified files in this cycle

### Backend

- `Shared/Operations/OperationalRecord.cs`, `MasterDataConfiguration.cs`, `MasterDataService.cs`, `DocumentNumbers.cs`.
- `Modules/Catalog/Product.cs`, `Modules/Suppliers/Supplier.cs`, `Modules/Warehouses/Entities.cs` and module READMEs.
- `Modules/Purchasing/Entities.cs`, `Contracts.cs`, `PurchasingConfiguration.cs`, `PurchaseOrderService.cs`, README.
- `Modules/Receiving/Entities.cs`, `Contracts.cs`, `ReceivingConfiguration.cs`, `ReceiptQueries.cs`, `ReceivingService.cs`, `ReceivingPosting.cs`, README.
- `Modules/Inventory/Entities.cs`, README.
- Controllers: `SuppliersController.cs`, `ProductsController.cs`, `WarehousesController.cs`, `AreasController.cs`, `RacksController.cs`, `LocationsController.cs`, `PurchaseOrdersController.cs`, `DeliveryNotesController.cs` (also ReceiptStockController).
- `Shared/Persistence/WrosDbContext.cs`, three new migration/designer pairs and the model snapshot.
- `Modules/Identity/PermissionCatalog.cs`, `PermissionAuthorization.cs`, `IdentityRegistration.cs`: additional centralized operational policies; existing auth architecture retained.
- `Modules/Audit/AuditService.cs`: shared safe correlation accessor; `Program.cs`: service registrations.

Backend paths above are relative to `src/Wros.Api`.

### Frontend

- `pages/MasterData.tsx`, `features/master-data/types.ts`, `hooks/useLookup.ts`.
- `pages/PurchaseOrders.tsx`, `PurchaseOrderEditor.tsx`, `PurchaseOrderDetails.tsx`, `features/purchasing/types.ts`.
- `pages/DeliveryNotes.tsx`, `DeliveryNoteEditor.tsx`, `DeliveryNoteDetails.tsx`, `ReceiptCorrection.tsx`, `ReceiptStock.tsx`.
- `features/receiving/types.ts`, `AllocationEditor.tsx`.
- `App.tsx`, `components/layout/AppShell.tsx`, `pages/Home.tsx`, `lib/presentation.ts`, `styles.css`.

Frontend paths above are relative to `src/wros-web/src`. Existing shared buttons, dialogs, cards/table/pagination, labels, skeletons, notices, confirmation and session components are reused. No dependency lockfiles were changed for these features.

### Documentation

- Root README operational links.
- `docs/phase-2-manual-checks.md`, `phase-3-manual-checks.md`, `phase-4-manual-checks.md`, and this guide.

Earlier staged Phase 1/password/seed and Phase 1.5 changes already present in the working tree were retained; they were not reset, committed or pushed as part of this cycle.

## Verification and owner acceptance

- .NET restore/build completed; builds passed with no warnings/errors.
- Frontend TypeScript/Vite production build passed.
- Each separate migration was generated and applied sequentially; EF migration listing confirms all three applied with prior migration history intact.
- API startup/authenticated read responses for master lists, PO list, DN list, reference placements/PO lookups and receipt stock were inspected against the migrated database; readiness reported Healthy. This inspection created normal login/logout audit entries, not sample business documents or balances.
- These engineering checks are **not owner manual acceptance**. No manual test is claimed passed on the owner's behalf and no automated test suite was added.

Use the detailed, numbered steps and expected results in:

1. [Phase 2 — master data](phase-2-manual-checks.md)
2. [Phase 3 — purchasing](phase-3-manual-checks.md)
3. [Phase 4 — receiving, correction and reconciliation](phase-4-manual-checks.md)

Remaining verification: real create/edit/send/receive/post/correct workflows, simultaneous operators, browser PDF appearance, real mobile keyboards/assistive technology and the SQL reconciliation checks in those guides. A fully consumed-source end-to-end scenario awaits a genuine later stock-out workflow; no test backdoor or ad-hoc balance editing is provided. Production deployment/least-privilege SQL accounts remain operational work outside this phase. All code changes stop at Phase 4; wait for owner manual results and explicit approval before Phase 5.
