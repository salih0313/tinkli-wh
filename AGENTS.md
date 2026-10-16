# WROS — Agent Development Instructions

> Version 1.1 | October 2026 | Source of truth for coding agents (Codex, Claude Code, etc.)

## 1. Mission and non-negotiable rules
Build WROS, a secure, fast, maintainable, multi-warehouse receiving and inventory system. WROS is the **physical inventory source of truth**. The legacy e-commerce application stays unchanged for now; integration is through documented APIs and an inbound authenticated event endpoint. Later, e-commerce may be incorporated as a WROS module without rewriting inventory.

- Build incrementally, **one phase at a time**. Stop after each phase; provide startup instructions, manual test scenarios, expected results, and ask the owner to verify before proceeding.
- **Do not add automated tests, test frameworks, or CI test pipelines now.** Manually verify each phase with the owner. Design code to be testable later.
- Prefer simple, explicit, production-grade solutions; avoid microservices, event brokers, CQRS/MediatR, elaborate abstractions, premature caching, or complex approval workflows.
- Do not silently invent business rules. Record unresolved decisions and ask the owner when they block a phase.
- Never modify the legacy e-commerce source or database without explicit authorization.
- No secrets, passwords, tokens, or production connection strings committed to source control.

## 2. Stack and architecture
- Backend: **.NET 10 LTS**, ASP.NET Core Web API with controllers, C#; EF Core 10, SQL Server.
- Frontend: React + TypeScript + Vite + Tailwind CSS; responsive desktop/tablet warehouse UI.
- Auth: ASP.NET Core Identity for users/passwords; short-lived access tokens validated server-side; secure refresh-token handling or secure HttpOnly cookie/BFF where practical. Never trust a client-supplied user ID. Read identity from validated claims.
- Observability: Serilog structured logs with correlation IDs, centralized exception handler/ProblemDetails, health checks. Add OpenTelemetry only when needed. Never log credentials or sensitive payloads.
- API documentation: OpenAPI in development; version endpoints under `/api/v1`.
- Deployment target: local development first (Mac or Windows), later Windows Server/IIS or Azure. Do not implement hosting until requested.
- Modular monolith with clear folders/modules: Identity, Catalog, Suppliers, Purchasing, Receiving, Warehouses, Inventory, Outbound, Integration, Audit, Shared. Keep controllers thin, business logic in application services, persistence in EF Core. Avoid generic repository wrapping EF Core.

## 3. Configuration and development experience
- Use `appsettings.json` for **configuration keys and non-secret local defaults**, with `appsettings.Development.json` for developer-specific non-secret overrides.
- Config sections: `ConnectionStrings:DefaultConnection`, `Jwt:Issuer`, `Jwt:Audience`, `Jwt:AccessTokenMinutes`, `Integration:AllowedClients`, `Logging`, `Cors:AllowedOrigins`, `Seed:Enabled`, `Inventory:ExpiryAlertDays` (default 30).
- Real database passwords, JWT signing keys, integration client secrets and seeded account passwords must come from .NET User Secrets (local) or environment variables/secret vault (deployed), **not plaintext appsettings committed to Git**. Provide an `appsettings.example.json` showing placeholders and a setup guide with exact commands.
- Use Options validation at startup. EF Core migrations are the only schema change path. Provide a simple README for `dotnet restore`, migrations, seeding, `dotnet run`, frontend setup, and manual API testing.

## 4. Identity, authorization and seeding
- Only **SuperAdmin** may create/manage user accounts, reset staff passwords and assign roles. No public registration.
- Seed roles idempotently: `SuperAdmin`, `WarehouseManager`, `WarehouseStaff`, `PurchasingStaff`, `InventoryController`, `Viewer`. Seed permissions and role-permission mappings centrally; permissions should include users.manage, roles.manage, suppliers.manage, products.manage, purchaseorders.manage, receiving.manage, inventory.view, inventory.adjust, inventory.transfer, outbound.manage, integration.manage, audit.view.
- Seed one SuperAdmin and one ordinary WarehouseStaff demo user **only in Development**, using credentials supplied through User Secrets/environment variables; do not embed a known default password. Provide commands for setting seed credentials. Seed role definitions in all environments, but do not create demo users in production.
- Roles govern operations; **all authorized users can access all warehouses in v1**. Design for future warehouse-scoped permissions without implementing them now.
- Enforce permissions on API endpoints, not just frontend menus. Audit user creation, permission changes and sensitive actions. Hash passwords with Identity; enforce password policy, lockout, rate limiting and secure reset process.
- Machine-to-machine integration uses a separate client identity (prefer OAuth2 client credentials or a validated signed client assertion) with minimal scopes; TLS mandatory. Rotate credentials and reject replays.

## 5. Core data model (suggested EF Core entities)
Use UUID/Guid or stable numeric IDs consistently; create unique business numbers separately. Include created/updated timestamps, actor IDs and row-version concurrency tokens where appropriate.

- `AppUser`, `AppRole`, `Permission`, `RolePermission`, `AuditLog`, `IntegrationClient`.
- `Supplier` (vendor contact and status).
- `Product` (SKU, name, brand, optional barcode, optional expiry-tracking flag); `Brand` optional as separate lookup. Product is **not** split by color/size/year in WROS v1.
- `PurchaseOrder`, `PurchaseOrderLine`, `PurchaseOrderRevision` (or auditable revision snapshot).
- `DeliveryNote`, `DeliveryNoteLine` (actual received items/quantities, independent of PO lines), `StockLot` (internal unique lot ID, optional supplier batch code and expiry), `DeliveryNoteAllocation` (lot, warehouse, storage location, quantity).
- `Warehouse`, `WarehouseArea`, `StorageRack`, `StorageLocation` (optional shelf/drawer position; hierarchy warehouse > area > rack > optional drawer).
- `InventoryBalance` (lot + location balance, unique key), `StockMovement` (immutable ledger with movement type, reference, quantity, location, actor, timestamp), `InventoryVersion`/snapshot generation.
- `SupplierReturn`, `SupplierReturnLine`, `Disposal`, `DisposalLine`, `WarehouseTransfer`, `WarehouseTransferLine`.
- `ExternalOrderDeduction`, `ExternalOrderDeductionLine`, `IntegrationInbox` (idempotency and request status), `InventorySyncSnapshot`, `InventorySyncSnapshotLine` (or equivalent immutable snapshot representation).

Create proper FK constraints, decimal/integer quantity rules (choose per UOM), indexes for SKU/barcode, warehouse/location, lot/expiry, movement history, integration event keys. Never rely on product name for matching.

## 6. Business rules
### Purchase orders
- Staff with purchasing permission can create, print/download and send a PO without manager approval.
- States: Draft, Sent, Locked/Closed (keep simple). Sent PO can be edited and resent with revision history **until any linked delivery note is created**. Lock once a delivery note exists; clarify exception behavior for canceled draft delivery notes before implementing unlock.
- PO is a **supplier request/reference, not a stock source**. Do not force ordered quantity to equal received quantity. One PO can have multiple delivery notes. No mandatory backorder workflow.

### Receiving and delivery notes — the only stock-in source in v1
- Create a delivery note from a PO by copying its lines as an editable starting point.
- Allow actual received quantity to be smaller, larger, zero; remove unreceived lines; add unexpected/substitute products, including new products. One general remarks field; do not require line-by-line reasons.
- Actual receipt and rack placement control stock. Allocate a received line across **multiple racks/locations**, possibly multiple lots; allocation totals must match actual received quantity before posting/closing.
- A product may be stored in multiple racks and multiple warehouses. Batch code and expiry are independently optional, except expiry is required when the product's expiry-tracking setting requires it. Every distinct receipt group has an internal StockLot ID.
- Draft edits do not change inventory. **Posting/closing** a verified, fully allocated delivery note creates inventory movements and available stock atomically. Partial PO fulfillment can be posted immediately; no wait for remaining quantities.
- Owner wants simple post-close rack corrections through the delivery note. Implement as **audited, atomic reallocation** that preserves total received stock and stock movement integrity; never silently overwrite a posted receipt or permit reallocation of already consumed stock. A separate rack-to-rack transfer UI is deferred.

### Inventory and expiry
- Track physical quantity per warehouse, location and lot, plus aggregate product/warehouse quantities. Ledger and balances must reconcile.
- Expose FIFO suggested picking order by receipt date, with FEFO information for expiring lots; the e-commerce picker decides actual lot/location selection. Support splitting one order line across multiple lots/racks.
- Configurable expiry alerts, default 30 days; show expired, expiring and relevant batch/location/quantity information. Exclude expired lots from sellable available quantity, but preserve physical on-hand stock and report it separately.
- Do not introduce warehouse-side checkout reservations in v1; legacy e-commerce already manages its own temporary checkout reservation (approximately five minutes).

### Stock-out and internal transfer
- E-commerce deduction: only when the existing e-commerce workflow reaches **Ready to Deliver**, after staff physically pick/pack. No warehouse deduction at cart/checkout/acceptance. E-commerce cancellation is not allowed after this state in the legacy workflow.
- Supplier return: record and deduct **after physical handover**; optional reference to original receipt for traceability, no mandatory reason-per-line.
- Disposal/damage: record and deduct **after physical disposal**.
- Warehouse-to-warehouse transfer: staff physically move goods first, then record a simple atomic transaction reducing source and increasing destination, preserving lot identity and overall company quantity. No approval/shipping/receiving workflow in v1.
- All movements must be transactional, permission-checked, audited and reject negative balances.

## 7. E-commerce integration contract
- Legacy e-commerce remains independent. WROS is authoritative for **physical** inventory, while e-commerce owns variant definitions (color/size/year), catalog enrichment, customer orders and temporary reservations.
- Provide authenticated, paginated **full inventory snapshot** API for a manual Sync button in the e-commerce admin. Include stable product/warehouse/location/lot IDs, SKU/barcode, brand/name, batch, expiry, on-hand and sellable quantity, plus a snapshot ID/version. The legacy app may choose which fields it consumes.
- Build a **consistent immutable snapshot** for all pages; if a sync fails halfway, the consumer must not publish partial data. Define snapshot expiry, ordering and pagination. Do not claim a normal paged live query is consistent.
- Offer an authenticated **ReadyToDeliver deduction endpoint** (the e-commerce system calls WROS; colloquially a webhook). Request includes external order ID, stable event/idempotency ID, and selected WROS lot/location IDs and quantities. Authenticate caller and validate permissions, identity mappings, stock and positive quantities.
- Deduct all selected lines atomically in one DB transaction; reject insufficient stock with a structured error and do not partially deduct. Store event key with unique constraint; retries return the prior result without deducting twice. Log correlation ID, never secrets.
- E-commerce should finalize Ready to Deliver only after confirmed WROS success. If legacy behavior cannot enforce that, document a reconciliation/failed-event process before production. Network timeout after commit must be safely retryable.
- Inventory snapshot must reflect supplier returns, disposals, transfers, expiries and successful order deductions. Incremental sync and push notifications are future enhancements.
- Do not assume e-commerce variants equal WROS products. Document mapping by stable external identifiers; never match by display name.

## 8. Auditing, security, correctness and performance
- Full user activity audit for create/update/delete, login/security actions, receipt posting, allocations, movements, sync operations and permission changes. Include actor from validated auth claims, timestamp UTC, action, entity/ref, safe old/new values, correlation ID. Redact passwords, tokens, secrets and sensitive PII.
- Database transactions for posting receipts, stock deductions and transfers; concurrency checks/locking prevent overselling. Unique idempotency keys prevent duplicate integration deductions. Avoid floating-point stock math.
- Validate all input server-side; parameterized EF queries, least-privilege SQL accounts, CORS allowlist, HTTPS, secure headers, throttling on login/integration, restrictive error responses and secret rotation.
- Structured logs, exception middleware, health endpoint, API request IDs, targeted SQL indexes and efficient paginated list queries. Do not log full integration bodies by default.
- Prefer correctness before optimization; profile actual slow queries. Do not introduce Redis, queues or microservices without evidence.

## 9. Frontend screens (v1)
Login; Dashboard (inventory/expiry/receiving summary); Users & Roles (SuperAdmin); Suppliers; Products; Warehouses/Areas/Racks/Drawers; Purchase Orders (create/edit/revisions/print/download); Delivery Notes (copy PO, edit actual items, split rack allocations, post, audit corrections); Inventory Search (product/warehouse/rack/lot/expiry); Stock Movements; Supplier Returns; Disposals; Warehouse Transfers; Expiry Alerts; Integration Status/Manual API documentation; Audit Viewer.

Design for fast keyboard entry, clear validation, mobile/tablet-friendly receiving, loading/error states and confirmation for irreversible posting. Use consistent reusable React components and a simple typed API client.

## 10. Migrations and seed requirements
- Use EF Core migrations from the first phase; no manual ad-hoc schema scripts as the normal workflow.
- Seed stable role/permission definitions idempotently (safe to rerun). Seed development SuperAdmin and WarehouseStaff accounts only if explicitly enabled and credentials are provided securely. Never reset existing passwords on every startup.
- Provide documented commands for initial migration, database update and future migrations, including Windows and Mac-friendly examples.
- Add development sample suppliers, products, warehouses, racks and optional example PO/DN **only with an explicit Development-only sample-data flag**; never populate demo transactions in production.
- Startup must fail clearly if mandatory production settings are missing. No destructive auto-migration or automatic database reset in production.

## 11. Delivery roadmap and manual acceptance gates
**Phase 0 — Foundation:** solution layout, API/frontend boot, SQL config, OpenAPI, exception handling, logging, health checks, migrations. Manual check: launch both apps, check health/OpenAPI, DB connection and error response.

**Phase 1 — Identity and seed:** Identity tables, roles/permissions, seeded dev users, login, secure auth, admin user management. Manual check: log in as SuperAdmin and staff, verify restricted endpoints, invalid login/lockout, create user, inspect audit.

**Phase 2 — Master data:** suppliers, products, warehouses, area/rack/location hierarchy. Manual check: CRUD, duplicates, validation, warehouse location navigation.

**Phase 3 — Purchasing:** PO draft/send/edit/revisions, print/download, lock when linked DN exists. Manual check: create PO, change and resend, review history, verify lock.

**Phase 4 — Receiving and stock-in:** DN from PO, flexible lines, lots/expiry, multi-rack allocation, posting, safe corrections. Manual check: order 10, receive 8; substitute/add/remove products; split 100 across racks; verify ledger and availability.

**Phase 5 — Inventory:** searchable warehouse/lot/rack balances, FIFO/FEFO suggestions, expiry alerts, audit history. Manual check: two batches in different racks; check quantities and alerts, expired exclusions.

**Phase 6 — Stock-out and transfer:** supplier returns, disposal and interwarehouse transfer. Manual check: deduct after physical completion, reject overdraw, verify transfer balances and ledger.

**Phase 7 — E-commerce integration:** authenticated snapshot API and ReadyToDeliver endpoint, idempotency, mapping, consistency. Manual check: retrieve all snapshot pages, compare totals; submit selected batches; retry identical event; simulate insufficient stock and invalid client; confirm no partial updates.

**Phase 8 — Hardening and handover:** performance profiling, authorization review, logging/trace review, operator guide, configuration guide and production readiness checklist. Manual check: role permissions, concurrent operations, pagination and recovery scenarios using owner-operated steps; no automated test suite.

At the end of **every phase**, agent MUST report: files changed; commands to run; configuration required; migration/seed commands; numbered manual test instructions with expected results; known limitations; and explicit request for owner approval. **Do not start the next phase until owner confirms.**

## 12. Open decisions — ask when relevant
- Exact supplier/PO numbering convention, tax/currency and UOM rules.
- Whether same SKU may appear as separate stock lots in one delivery line and how supplier-provided batch numbers are entered.
- Specific permissions for each staff role; role list above is a starting proposal, not a finalized organization policy.
- E-commerce's actual API contract and how selected WROS lot/location IDs will reach the legacy e-commerce system without modifying it now.
- Whether product-level expiry tracking is mandatory for particular categories and how unknown expiry should be handled.
- Behavior when correcting a posted DN whose stock has already been consumed.
- Production identity provider, token storage architecture and operational credential rotation details.

## 13. First instruction to coding agent
Read this entire AGENTS.md. Do not implement the entire system at once. First inspect the repository and summarize existing files. If empty, propose the exact solution structure and execute **Phase 0 only**. Use the stack and simplicity constraints above. Include all setup/configuration/migration instructions and a manual test checklist. Stop and wait for owner verification before Phase 1. If a blocking ambiguity exists, ask one focused question rather than inventing behavior.
