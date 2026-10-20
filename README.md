# WROS — Warehouse Receiving & Operations

WROS is the physical inventory source of truth: a .NET 10 / EF Core 10 / SQL Server modular monolith with React, TypeScript, Vite and Tailwind. Phases 0–8 and the Phase 3.1 supplier-catalog enhancement are implemented; **owner manual acceptance of the new work remains pending**. No automated test suite, production deployment or legacy e-commerce source/database change is included.

Staff use ASP.NET Identity with secure HttpOnly, SameSite Strict, fixed-lifetime cookies and same-origin CSRF-protected mutations. Machine integration uses a separate scoped, single-use RS256 signed client assertion. There is no competing staff token mechanism, microservice, queue, cache or generic repository.

## Implemented modules

- Identity: login/lockout, account activation, password changes, SuperAdmin staff/role administration, live database permissions, security audit.
- Master data: suppliers, products, warehouse > area > rack > optional location, active status and concurrency checks.
- Supplier catalog: bounded XLSX upload/mapping/preview, transactional imports/history, explicit product mapping, manual/catalog selection, discounted pricing snapshots and compact quantity preparation. [Phase 3.1 delivery](docs/phase-3.1-delivery.md) and [manual acceptance/setup](docs/phase-3.1-manual-checks.md).
- Purchasing: year-based PO numbers, drafts/send/resend, revision snapshots, printable/browser Save as PDF, lock when receiving begins; sharing is manual.
- Receiving: standalone/copied DN, flexible actual quantities/products/lots, expiry and split allocations; atomic posting and safe available-stock rack correction.
- Inventory: physical/sellable/expired dashboard, product/location/lot explorer, hierarchy/batch filters, FIFO/FEFO suggestions, expiry alerts and immutable global ledger.
- Completed stock movements: supplier returns after handover, damage/disposal after disposal, interwarehouse transfers after physical movement; atomic balances, preserved provenance, duplicate submission protection.
- Integration: machine registry/scopes, stable product mappings, consistent immutable full snapshots, atomic idempotent Ready-to-Deliver deductions and operational history.

Only whole units are supported. Expired stock remains physical but is not sellable; expiry today remains valid through UTC midnight. WROS does not own e-commerce checkout reservations or variant definitions. No low-stock thresholds have been configured; dashboard explicitly reports this.

## Quick local setup

Required: .NET **10 SDK**, SQL Server 2022+ reachable over TCP (Mac/Linux cannot use LocalDB), Node **22.12+**, npm, trusted ASP.NET development certificate. Exact versions are locked in global.json, the local tool manifest and package lockfiles.

Keep your existing SQL database and User Secrets. For a new setup see [local development](docs/local-development.md); use `appsettings.example.json` only as a placeholder reference. Never commit real connection passwords, caller private keys or account credentials.

From repository root:

```sh
dotnet dev-certs https --trust
dotnet restore Wros.slnx --locked-mode
dotnet tool restore
dotnet build Wros.slnx --no-restore
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet run --project src/Wros.Api --launch-profile https
```

Another terminal:

```sh
npm --prefix src/wros-web ci
npm --prefix src/wros-web run dev
```

Open **http://localhost:5173**. Vite proxies same-origin `/api` to **https://localhost:7243**. API health: `/api/v1/health/live` and `/api/v1/health/ready`. Development OpenAPI: `/openapi/v1.json`. If connection fails, trust the local HTTPS certificate and check SQL readiness; don't disable cookie security. Production SPA/API must share an HTTPS origin; deployment remains separately authorized.

Production build verification:

```sh
dotnet build Wros.slnx -c Release --no-restore
npm --prefix src/wros-web run build
```

VS Code: use the existing local WROS API Development launch profile if present; `.vscode` is intentionally ignored. Stop a running API before F5 uses ports7243/5243.

## Migrations and Development users

All previous migrations/data are retained. New additive migrations:

- `20261018134244_Phase6CompletedStockMovements`
- `20261018134809_Phase7Integration`
- `20261019062315_Phase31SupplierCatalogPricing`

Phase5 is read-only; Phase8 requires no additional schema migration. No demo master data, documents, stock or integration clients are seeded. [Migration instructions and read-only reconciliation SQL](docs/database-migrations.md).

The owner-approved local C# Development seed flow is preserved. If accounts are missing, copy the **empty** `src/Wros.Api/Modules/Identity/DevelopmentSeedCredentials.example.cs` to `DevelopmentSeedCredentials.local.cs`, supply your own minimum-eight-character passwords in the ignored file, and run the Development database-update command above. No seed environment flag is required. Never populate the tracked example. Existing users/passwords/role edits are skipped, not reset. Release builds exclude the local credential source and compile the empty example; use the default Debug migration command for local account seeding. Production never registers the demo-user hook.

Development usernames: `wros.admin` (SuperAdmin), `wros.staff` (WarehouseStaff). Seeded roles: SuperAdmin, WarehouseManager, WarehouseStaff, PurchasingStaff, InventoryController, Viewer. There is no public registration. Existing staff grants are preserved (initial inventory.view only); SuperAdmin assigns operational permissions. All warehouses remain shared in v1. [Role and permission reference](docs/roles-and-permissions.md).

## Machine integration setup

Disabled until an active registry client, explicit allowlist, public RSA key and scope are configured. Private keys stay with the caller. Follow [the e-commerce integration guide](docs/e-commerce-integration.md) for exact public-key User Secrets commands, assertion utility, curl examples, mappings, immutable pagination/hash/version protocol, RTD idempotency and reconciliation. The legacy developer must implement this consumer contract; WROS does not modify or deploy that application.

## Project structure

```text
src/Wros.Api/
  Controllers/                         Versioned thin API controllers
  Modules/
    Identity/ Audit/ Catalog/ Suppliers/ Warehouses/
    Purchasing/ Receiving/ Inventory/ Outbound/ Integration/
  Shared/
    Configuration/ Errors/ Health/ Observability/ Operations/
    Persistence/WrosDbContext.cs
    Persistence/Migrations/            Append-only migration history
src/wros-web/src/
  api/ auth/                           Typed client and existing session flow
  components/                          Shared forms/UI/feedback/tables/layout
  features/                            Master-data, purchasing, receiving patterns
  hooks/                               Lookups, confirmation, resource loading
  pages/                               Existing and new permission-aware screens
scripts/integration-assertion.mjs       Caller-side local signing utility
```

The only newly direct runtime dependency is Microsoft.IdentityModel.JsonWebTokens 7.7.1, already present transitively at that version; its validated JWT API is now explicitly referenced. No new frontend dependency or automated test framework was added.

## Handover and owner acceptance

- [Phases 5–8 delivery record and changed files](docs/phases-5-8-delivery.md)
- [Phase5 inventory manual checks](docs/phase-5-manual-checks.md)
- [Phase6 stock-out/transfer manual checks](docs/phase-6-manual-checks.md)
- [Phase7 integration manual checks](docs/phase-7-manual-checks.md)
- [Phase8 security, regression and responsive checks](docs/phase-8-manual-checks.md)
- [Configuration](docs/configuration.md), [local setup](docs/local-development.md), [migrations](docs/database-migrations.md)
- [Warehouse operator guide](docs/warehouse-operations.md), [troubleshooting](docs/troubleshooting.md)
- [Production readiness and remaining operational decisions](docs/production-readiness.md)

Earlier Phase0/1/1.5/2/3/4 guides are retained for regression. Builds/read-query inspections do not establish owner acceptance, real-volume performance or legacy cross-system behavior. Current limitations include retained expired snapshot/replay records pending an approved maintenance policy, deployment/key-ring planning, no arbitrary completed-transaction reversal workflow, and no configured low-stock thresholds.

**Stop after Phase 3.1. Owner manual results and explicit approval are required before production or further scope.**
