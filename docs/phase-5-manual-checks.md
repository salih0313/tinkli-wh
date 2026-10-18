# Phase 5 — Inventory acceptance

Owner-operated checks; implementation builds do not constitute manual acceptance.

## Setup and commands

.NET 10 SDK, Node 22+, SQL Server and the existing Development User Secrets are required. Preserve the database. No Phase 5 schema changes or sample stock seeds are needed.

```sh
dotnet restore Wros.slnx
dotnet tool restore
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet run --project src/Wros.Api --launch-profile https
```

In another terminal:

```sh
cd src/wros-web
npm ci
npm run dev
```

Open http://localhost:5173 (use the URL printed by Vite), trust the local certificate as described in README, and log in with your existing SuperAdmin. Set `Inventory:ExpiryAlertDays` in appsettings.Development.json; default 30. No low-stock thresholds currently exist; the dashboard states this rather than inventing a policy.

## Checks

1. Through Delivery Notes, receive the same product into two warehouses and several racks. Use one older receipt with later expiry and one newer receipt with earlier valid expiry, plus an expired lot. Post each receipt. Expected: only posted quantities appear in Inventory overview and explorer; drafts and POs add no stock.
2. Compare overview warehouse and product totals against received units. Expected: physical includes expired units; sellable excludes expired and inactive products. All quantities are whole units, not financial values.
3. Search SKU/name/barcode/brand and combine warehouse/area/rack/lot UUID, batch and expiry filters. Expected: matching placements only; invalid UUID produces validation error. Clear filters to restore stock. Switch product grouping and expand a product. Expected: its paged lot/location details appear without altering stock.
4. Sort FIFO, then FEFO with **Sellable only** selected. Expected: FIFO oldest receipt first, FEFO nearest nonexpired expiry first with undated lots last; ties deterministic. Recommendations never reserve/select/deduct inventory. The API `/api/v1/inventory/recommendations?sort=fefo&productId=UUID` always excludes expired stock.
5. Open Expiry alerts; choose near expiry and expired in turn. Expected: configured UTC date window, batch/location/quantity visible, expired physical remains present. A lot expiring today remains sellable until the UTC date changes.
6. Open Stock movements; filter type and search SKU/receipt/correlation. Expected: signed immutable entries, responsible actor/time/reference, no edit control; pages remain bounded.
7. Log in as inventory-view-only staff. Expected: inventory screens accessible, stock mutation endpoints forbidden. Remove inventory.view in Roles, refresh staff session/navigation, request `/api/v1/inventory` directly. Expected: 403. Anonymous request returns 401.
8. At 375, 768, 1024 and 1440 pixels, portrait and landscape, use keyboard and touch navigation. Expected: mobile cards, visible focus, readable filters and paging, no page horizontal overflow.

## Read-only database verification

```sql
SELECT StockLotId, WarehouseId, RackId, LocationId, Quantity FROM InventoryBalances ORDER BY StockLotId;
SELECT StockLotId, WarehouseId, RackId, LocationId, SUM(Quantity) AS LedgerQuantity
FROM StockMovements GROUP BY StockLotId, WarehouseId, RackId, LocationId;
SELECT Id, ProductId, SupplierBatch, Expiry, ReceivedAt FROM StockLots;
```

Expected: ledger sums reconcile with each balance, including zero rows; only posted lots contribute balances. Use UTC dates when comparing expiry.

## Troubleshooting

401: log in again. 403: grant inventory.view using SuperAdmin. Empty stock: post a fully allocated DN. Connection failed: start API, trust HTTPS certificate, check readiness. No stock figures should be inserted manually into SQL.
