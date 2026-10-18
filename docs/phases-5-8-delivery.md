# Phases 5–8 delivery record

Scope authorized in the pasted Phases5–8 request: sequential implementation/build gates in one cycle, stop after8. Existing Phases0–4 business/authentication behavior and database data are preserved. No automated tests/frameworks, production deployment, warehouse reservations, legacy edits, microservices or brokers were introduced. Owner manual acceptance is pending.

## Phase5 — Inventory visibility

InventoryQueries and InventoryController provide dashboard SQL aggregates, physical/sellable/expired whole units, warehouse totals, expiry attention, paged/filterable balance rows, product grouping, FIFO/FEFO read recommendations and immutable global ledger. UI: Inventory overview, explorer, product expansion, expiry alerts and movements. Shared typed resource/debounce hooks reuse existing forms/cards/tables/mobile layout. No stock writes or Phase5 migration. No export was defined by AGENTS; no extra inventory export added. No thresholds currently configured, so low-stock classification is explicitly unconfigured.

APIs (inventory.view): `GET /api/v1/inventory`, `/products`, `/dashboard`, `/recommendations`, `/movements`. Existing `/api/v1/receipt-stock` remains compatible. Filter keys: search, productId, warehouseId, areaId, rackId, lotId, batch, expiry(all/expired/near/sellable), sort(sku/fifo/fefo/quantity), page/pageSize. Recommendations force sellable-only. Ordinary staff inventory pages are live, not immutable snapshots; only Phase7 snapshot APIs promise capture consistency.

## Phase6 — Completed stock-out and transfers

Shared **StockTransaction + StockTransactionLine** entities explicitly distinguish SupplierReturn, Disposal and Transfer instead of duplicating identical completed-document infrastructure three times. Source balance references preserve lot/receipt provenance; destination splits have independent selections. Numbering SR/DS/WT-UTCyear-sequence follows the already approved yearly PO/DN convention. No pending/approval workflow. Requests confirm physical completion and carry stable UUID idempotency keys plus a server request hash; conflicting reuse rejects. Returns/disposals subtract, interwarehouse transfers append both signed sides in a serializable transaction; row-version checks and nonnegative SQL constraints protect balances. Original DN allocation history stays unchanged.

APIs: `GET/POST /api/v1/stock-transactions/{kind}`, `GET .../{kind}/{id}`, with kind SupplierReturn/Disposal/Transfer. Exact permissions centrally mapped: outbound.manage / inventory.adjust / inventory.transfer. Source lookup endpoints `/balances`, `/placements`, `/suppliers` accept any of those operation grants; list/post/detail additionally enforce exact kind. UI: lists, completed-movement forms, read-only details and confirmation, paged/searchable source selection and multiple destination splits.

Migration `20261018134244_Phase6CompletedStockMovements`: headers/lines, ledger reference fields, timestamp/reference indexes, new signed movement types. No demo records/seeds/grant changes.

## Phase7 — E-commerce boundary

New entities: IntegrationClient, ProductMapping, AssertionReplay, InventorySyncSnapshot, InventorySyncSnapshotLine, ExternalOrderDeduction, ExternalOrderDeductionLine. StockMovement adds IntegrationClientId and reference metadata; InventoryBalance keeps its existing schema and is now also updated by completed stock-out/transfer/integration services. Existing InventoryBalance/StockLot/receipt/Identity tables remain intact. External deduction header's unique client/event + request hash/prior result serves as the durable idempotency inbox; client/order uniqueness prevents accidental second-event deduction.

Machine authentication: direct RS256 signed assertion per HTTPS request, exact audience/issuer/subject/type/kid, <=120-second assertion lifetime, persistent unique client/jti replay key, configured scopes and active registry. Staff cookie/CSRF logic stays unchanged; only an explicitly validated machine principal bypasses staff antiforgery. Caller owns private keys; WROS stores configured public keys. Existing transitive Microsoft.IdentityModel.JsonWebTokens 7.7.1 is now explicitly referenced, with no version change; no new frontend packages.

Snapshots are materialized under serializable SQL isolation and serialized capture application lock, immutable rows, monotonic capture version, expiry, count, deterministic ordinals and SHA256. Saved pages don't query live availability. FIFO/FEFO ranks are per product/lot and frozen; zero/absent rows mean zero in a validated full replacement. Product mappings are explicit per client; multiple external variants may share physical stock without duplicating it. External product fields previously entered in catalog are retained and are not automatically trusted as client mappings.

Machine endpoints: `POST /api/v1/integration/snapshots`, `GET .../snapshots/{id}?page=&pageSize=` under inventory.snapshot; `POST /api/v1/integration/ready-to-deliver` under orders.deduct. The latter validates line sums, positive units, stable mapped product/lot/warehouse/rack/position IDs, expiry, active hierarchy and combined availability, then deducts all allocations atomically. Identical business event retry returns original result; conflict409 never deducts again. Machine client/order/event/correlation are retained.

Staff endpoints (integration.manage): `GET /api/v1/integration-management`, `POST .../clients`, `GET/POST .../mappings`, `PUT .../mappings/{id}`. UI: registry activation, mapping maintenance and recent paged deductions/snapshots plus API contract notes. No machine credential entry in the browser.

Migration `20261018134809_Phase7Integration` adds the integration schema and ledger client/type support. No callers/keys/mappings seeded. [Complete integration contract](e-commerce-integration.md) includes signing utility, secure local public-key configuration, curl bodies, errors, complete snapshot publication, version protection, Ready-to-Deliver success gate and timeout/reconciliation steps. Legacy implementation is a separately owned consumer task, not a modification made here.

## Phase8 — Review and handover

Backend policy review, exact kind authorization denial audit, safe error/correlation behavior, frame/no-store/nosniff protections, validated integration options, fixed staff security retained. Private Development seed source excluded from Release; tracked seed example cleared. Existing database passwords not reset; rotate any previously used known example values separately. No additional schema changes needed.

Read-only inventory projections/SQL aggregation, bounded pagination, bulk source/destination validation, no eager hidden product-detail requests, debounced searches and immutable history guards improve maintainability/correctness. Existing timestamp/reference, lot/expiry/location and unique event/order indexes reviewed. No production scale benchmark or hosting rollout claimed. Existing full PO revision-array API is preserved for compatibility; see limitations below.

## Verification evidence

- Phase5 .NET build: success, 0 warnings/errors; frontend production build: success before Phase6.
- Phase6 .NET build and frontend build: success before Phase7; migration generated additively.
- Phase7 .NET build and frontend build: success before Phase8; migration generated additively.
- Both new migrations applied to the existing local SQL development database; database update completed successfully. Earlier migration IDs retained; no reset.
- Temporary **read-only developer query inspection outside the repository** executed inventory SKU/FIFO/FEFO pages, product aggregation, dashboard, ledger, all completed transaction histories, integration status and mappings. No test suite or fake stock fixture added. Local database had two users, one product and no balance/movement records, so this verifies SQL translation, not populated-stock behavior or scale.
- Initial local timing evidence: cold inventory query ~1.6s; subsequent FIFO/FEFO ~34–48ms, product aggregation ~95ms, dashboard ~127ms, ledger ~4ms, stock transaction lists ~2–19ms, integration status ~38ms, mapping page ~15ms. These sparse-data readings are diagnostic, not a performance target/benchmark.
- API liveness/readiness and Development OpenAPI returned200. Anonymous inventory, disposal history, integration management and machine snapshot POST returned401 with correlation IDs. This is a developer endpoint smoke check, not manual functional acceptance.
- .NET locked restore and npm ci passed. NuGet vulnerability check including transitive packages and full npm audit reported no known vulnerabilities at review time; this is not an ongoing security guarantee.
- Final Release .NET build and React production build verified separately; no automated unit/integration/E2E tests run or installed. `git diff --check` clean.

## Configuration and commands

See [local setup](local-development.md), [configuration](configuration.md), [database guide](database-migrations.md) and [integration setup](e-commerce-integration.md). Existing SQL secrets/account credentials remain local. Integration defaults disabled until explicitly registered/configured. No new demo user, master or transaction seed is required.

```sh
dotnet restore Wros.slnx --locked-mode
dotnet tool restore
dotnet build Wros.slnx --no-restore
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet run --project src/Wros.Api --launch-profile https
# Separate terminal:
npm --prefix src/wros-web ci
npm --prefix src/wros-web run dev
# Production build only, not deployment:
dotnet build Wros.slnx -c Release --no-restore
npm --prefix src/wros-web run build
```

Browser: http://localhost:5173; API https://localhost:7243. Development seed accounts retain wros.admin/wros.staff usernames and existing passwords; roles unchanged. No new staff permission names/grants. Machine scopes inventory.snapshot/orders.deduct are separate from staff roles.

## Owner acceptance and limitations

Run [Phase5](phase-5-manual-checks.md), [Phase6](phase-6-manual-checks.md), [Phase7](phase-7-manual-checks.md), [Phase8](phase-8-manual-checks.md) numbered checklists. Expected results, negative/security/concurrency cases, exact setup, read-only SQL, 375/768/1024/1440 widths, mobile/tablet/desktop and troubleshooting are included. Every existing screen is in the Phase8 regression list. None of these owner tests are claimed passed.

Pending owner/operational decisions: actual legacy caller/mapping convention and cross-system gate/reconciliation, production credentials/hosting/Data Protection, explicit role grants, snapshot/replay retention maintenance and real-volume profile, low-stock policy if desired. Expired snapshot and replay records currently remain stored; idempotency business history must not be purged. Source generation must be reconciled after restoring an older DB. Current legacy PO revision-array endpoint remains unbounded to preserve compatibility; a future paged contract can be introduced with its consumer update if revision volume warrants it. Live dashboard/list aggregates can represent successive committed moments; capture snapshots are the consistency boundary. Existing stock corrections cannot rewrite unavailable/consumed stock. No arbitrary reversal or e-commerce cancellation workflow was invented.

**Stopped after Phase8. Please provide owner manual results/approval before any additional scope or deployment.**

## Files changed

The manifest includes new and modified delivery files; build artifacts, private credentials, local formatter cache and temporary read-only inspection code are excluded.

- `.editorconfig`
- `README.md`
- `docs/configuration.md`
- `docs/database-migrations.md`
- `docs/e-commerce-integration.md`
- `docs/local-development.md`
- `docs/phase-5-manual-checks.md`
- `docs/phase-6-manual-checks.md`
- `docs/phase-7-manual-checks.md`
- `docs/phase-8-manual-checks.md`
- `docs/phases-5-8-delivery.md`
- `docs/production-readiness.md`
- `docs/roles-and-permissions.md`
- `docs/troubleshooting.md`
- `docs/warehouse-operations.md`
- `scripts/integration-assertion.mjs`
- `src/Wros.Api/Controllers/IntegrationController.cs`
- `src/Wros.Api/Controllers/InventoryController.cs`
- `src/Wros.Api/Controllers/StockTransactionsController.cs`
- `src/Wros.Api/Controllers/SystemController.cs`
- `src/Wros.Api/Modules/Identity/DevelopmentSeedCredentials.example.cs`
- `src/Wros.Api/Modules/Identity/IdentityRegistration.cs`
- `src/Wros.Api/Modules/Identity/PermissionCatalog.cs`
- `src/Wros.Api/Modules/Integration/Entities.cs`
- `src/Wros.Api/Modules/Integration/IntegrationAuthentication.cs`
- `src/Wros.Api/Modules/Integration/IntegrationService.cs`
- `src/Wros.Api/Modules/Integration/README.md`
- `src/Wros.Api/Modules/Inventory/Entities.cs`
- `src/Wros.Api/Modules/Inventory/InventoryQueries.cs`
- `src/Wros.Api/Modules/Inventory/README.md`
- `src/Wros.Api/Modules/Outbound/Entities.cs`
- `src/Wros.Api/Modules/Outbound/README.md`
- `src/Wros.Api/Modules/Outbound/StockTransactionService.cs`
- `src/Wros.Api/Modules/Receiving/ReceivingConfiguration.cs`
- `src/Wros.Api/Modules/Receiving/ReceivingPosting.cs`
- `src/Wros.Api/Program.cs`
- `src/Wros.Api/Shared/Configuration/FoundationOptions.cs`
- `src/Wros.Api/Shared/Observability/CorrelationMiddleware.cs`
- `src/Wros.Api/Shared/Persistence/Migrations/20261018134244_Phase6CompletedStockMovements.Designer.cs`
- `src/Wros.Api/Shared/Persistence/Migrations/20261018134244_Phase6CompletedStockMovements.cs`
- `src/Wros.Api/Shared/Persistence/Migrations/20261018134809_Phase7Integration.Designer.cs`
- `src/Wros.Api/Shared/Persistence/Migrations/20261018134809_Phase7Integration.cs`
- `src/Wros.Api/Shared/Persistence/Migrations/WrosDbContextModelSnapshot.cs`
- `src/Wros.Api/Shared/Persistence/WrosDbContext.cs`
- `src/Wros.Api/Wros.Api.csproj`
- `src/Wros.Api/appsettings.example.json`
- `src/Wros.Api/appsettings.json`
- `src/Wros.Api/packages.lock.json`
- `src/wros-web/src/App.tsx`
- `src/wros-web/src/components/layout/AppShell.tsx`
- `src/wros-web/src/features/inventory/LedgerTable.tsx`
- `src/wros-web/src/features/inventory/StockTable.tsx`
- `src/wros-web/src/features/inventory/types.ts`
- `src/wros-web/src/hooks/useConfirm.tsx`
- `src/wros-web/src/hooks/useDebouncedValue.ts`
- `src/wros-web/src/hooks/useResource.ts`
- `src/wros-web/src/pages/Home.tsx`
- `src/wros-web/src/pages/Integration.tsx`
- `src/wros-web/src/pages/Inventory.tsx`
- `src/wros-web/src/pages/InventoryDashboard.tsx`
- `src/wros-web/src/pages/ReceiptStock.tsx`
- `src/wros-web/src/pages/StockMovements.tsx`
- `src/wros-web/src/pages/StockTransactions.tsx`
