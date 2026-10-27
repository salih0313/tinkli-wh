# Inventory UI improvements

## Scope and changed files

- `src/wros-web/src/pages/Inventory.tsx`: default product totals, compact expandable rows, view switch, filter reset, empty/error states, refreshed expanded stock.
- `src/wros-web/src/pages/InventoryDashboard.tsx`: overview metrics, expiry attention, linked warehouse stock cards, recent movements.
- `src/wros-web/src/pages/StockMovements.tsx`: navigation, readable movement labels, refresh and search.
- `src/wros-web/src/features/inventory/InventoryNav.tsx`: shared inventory navigation.
- `src/wros-web/src/features/inventory/StockTable.tsx`: readable product/location/receipt information and separate quantity columns.
- `src/wros-web/src/features/inventory/LedgerTable.tsx`: readable movement names and signed quantity colors.
- `src/wros-web/src/App.tsx`: separate inventory/expiry component lifetimes so their default filters reset when navigating between them.
- `src/wros-web/src/styles.css`: responsive inventory layout and component styles.
- `src/Wros.Api/Modules/Inventory/InventoryQueries.cs`: grouped product sorting now respects largest quantity.

## Run

Use existing local configuration and database. No new settings, migrations or seeds are required. No inventory mutation rules changed.

```sh
dotnet build Wros.slnx --no-restore
npm --prefix src/wros-web run build
dotnet run --project src/Wros.Api --launch-profile https
```

In another terminal:

```sh
npm --prefix src/wros-web run dev
```

## Owner verification

1. Open Inventory overview. Expect on-hand, sellable, expired and catalog-product cards. Catalog products explicitly include products without stock. Warehouse cards show on-hand/sellable quantities. Empty databases show useful empty states.
2. Open Products & stock. Expect product grouping by default, with compact rows and aligned On hand, Sellable and Unavailable columns. Each SKU appears once per filtered result, even with multiple DNs.
3. Expand a product received through multiple DNs. Expect separate receipt/location rows. The product total equals the sum of all its remaining matching placements, including older receipts. Internal UUIDs must not appear.
4. Filter by product, batch, expiry, warehouse, area and rack. Expect product totals and expanded locations to use the same filters. Clear filters restores the page defaults. Select Largest quantity and expect products in descending on-hand order across all result pages.
5. Switch to Locations. Expect individual placements and FIFO/FEFO order options. Selecting FIFO/FEFO switches to sellable stock. Switch back to Products and expect SKU sorting with no FIFO/FEFO option.
6. Click a warehouse card from the overview. Expect inventory restricted to that warehouse, with the warehouse name in the filter summary. Open location filters to change it.
7. Open Expiry alerts from inventory navigation. Expect approaching-expiry placements by default. Choose Expired and expect on-hand quantities retained, with zero sellable units. Navigate back to Products & stock and expect normal product defaults.
8. Refresh while a product is expanded. Expect both product totals and expanded placements to reload. Search for an unknown product and expect a clear empty state. If the API is unavailable, expect an unavailable state and a usable Refresh action.
9. Open Movements. Expect readable movement names, signed quantities, search/type filters and refresh. Use each navigation tab and verify the active page is indicated.
10. Repeat at phone/tablet widths and at 200% zoom. Expect readable product cards, accessible quantity labels, usable keyboard expansion and no page-wide horizontal overflow. Wide placement tables may scroll within their container.

## Limitations and acceptance

Build verification does not confirm visual behavior against live owner data. No browser automation or automated tests were added. Overview figures span all warehouses; product-list figures reflect active filters. Inventory excludes zero-balance placements as before. FIFO/FEFO remain placement suggestions; low-stock thresholds are not configured or introduced here.

Please verify these scenarios and approve this inventory UI update before further phase work.
