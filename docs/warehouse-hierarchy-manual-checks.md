# Warehouse cards and compact tree review

`/warehouses` shows summary cards; `/warehouses/:warehouseId/manage` opens one warehouse's fully expanded tree. Counts include active and inactive descendants. Search/status filters apply to overview cards. Actions appear on node hover or keyboard focus and remain visible on touch devices. Existing area/rack/location routes remain available.

Changed files: web `src/pages/WarehouseHierarchy.tsx`, `src/pages/MasterData.tsx`, `src/hooks/useLookup.ts`, `src/App.tsx`, `src/styles.css`; API `Controllers/WarehousesController.cs`, `Shared/Operations/MasterDataService.cs`; this guide.

The existing master-data read policy protects the new GET `/api/v1/warehouses/counts` and GET `/api/v1/warehouses/{id}/structure` endpoints. Counts are projected in SQL. Structure returns the selected warehouse and its descendants using a fixed number of queries; it does not fetch each node separately. Structure is a current setup view, not an immutable inventory snapshot.

Start/restart the API with `dotnet run --project src/Wros.Api`; start the frontend with `npm --prefix src/wros-web run dev`. No new settings, migrations, seed steps or dependencies are required. Build checks: `dotnet build Wros.slnx --no-restore` and `npm --prefix src/wros-web run build`. No automated tests added.

1. Open Warehouses. Expect compact cards showing code/name/status and area, rack and location counts. Compare counts with known data, including inactive children; an empty warehouse shows zero. Search by code/name and filter active/inactive.
2. Open Manage warehouse (or View structure for read-only users). Expect a separate URL, the warehouse name, a back link and all area/rack/location branches expanded initially. Reload the URL directly; expect the same warehouse. An invalid ID should show the API error with a back link.
3. Hover each node. Expect add-child/edit/deactivate actions only on that row. Tab through the tree: actions become visible on keyboard focus. Toggle individual branches with the labeled arrow buttons.
4. Add an area under a warehouse, a rack under an area and a location under a rack. Expect correct parent context and new nodes under the selected parent after save. Edit a name/code/description. Expect updates without closing other branches. Return to the cards and verify refreshed counts.
5. Try duplicate codes within the same parent and blank required fields. Expect validation; server errors leave the dialog open. Cancel preserves existing data.
6. Deactivate an unused node, then reactivate it. Expect confirmation and updated status without changing children. Try deactivating a hierarchy holding stock: expect the API rejection and preserved data/history.
7. Sign in without `warehouses.manage` but with master-data read permission. Expect counts and tree viewing, without add/edit/deactivate actions. Existing write endpoint permissions remain enforced.
8. At mobile and touch-tablet sizes, expect actions to remain visible with touch-friendly targets, wrapped rows and compact indentation. Check the account dialog and back navigation.
9. Stop the API and refresh. Expect an error; restart it and use Refresh to recover. Revisit legacy area/rack/location routes and verify scope and parent selection on creation.

Limitations: physical deletion remains unsupported; deactivate/reactivate preserves history. Counts represent setup records, not inventory quantities. Large trees render the selected warehouse's full structure. Browser visual and authenticated CRUD checks remain owner-operated. Please complete these checks and approve the update.
