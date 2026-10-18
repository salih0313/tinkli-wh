# Phase 2 — Master data manual acceptance

## Prerequisites and commands

Use .NET 10, SQL Server, Node >=22.12, the existing Development connection secret and Phase 1 accounts. Keep the existing database; do not drop/recreate it. From the repository root:

```sh
dotnet restore Wros.slnx
dotnet tool restore
dotnet build Wros.slnx
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet run --project src/Wros.Api --launch-profile https
```

Second terminal:

```sh
npm ci --prefix src/wros-web
npm run dev --prefix src/wros-web
```

Open http://localhost:5173. Production build: `npm run build --prefix src/wros-web`.

Migration: `Phase2MasterData`. Existing baseline/Identity migrations are preserved. No sample suppliers/products/warehouses or balances are seeded. Only the `warehouses.manage` permission is added idempotently and granted to SuperAdmin. Existing staff mappings are preserved; use Roles to grant operational permissions deliberately. There are no new secrets or settings.

Master data read access requires at least one of suppliers.manage, products.manage, warehouses.manage, purchaseorders.manage, receiving.manage or inventory.view. Writes require the corresponding manage permission. All warehouses remain accessible in v1; no per-warehouse authorization is introduced. Products use whole units (owner decision), no variants/prices/tax rules. SKU is exposed as Code in the shared master API.

## Numbered manual checks (owner execution required)

1. Sign in as SuperAdmin; visit Suppliers, Products and Warehouses, then the location hierarchy. Expected: existing shell/navigation and login/logout work; initial lists have useful empty states. No fake master data or stock is inserted.
2. Create supplier SUP-001 with contact, phone, email, address, description and remarks. Edit, reopen and search by code/name; filter Active/Inactive. Expected: values persist, status filters/search work, invalid email and blank/overlong code/name are rejected. Supplier contact PII is not copied to audit payloads.
3. Create another supplier with code `sup-001` or surrounding whitespace. Expected: 409 duplicate conflict; code comparisons are canonical uppercase/trimmed. Rejected changes do not create another supplier.
4. Create product SKU-001 with barcode, optional brand, description, external product ID and expiry tracking. Edit and view it. Expected: no color/size/year variants; no stock quantity input; whole-unit model; saving never creates a stock balance or movement. Names may repeat with distinct SKUs. Duplicate SKU/external product ID is rejected. Barcode is indexed but is not required to be unique.
5. Create warehouse WH-01; open its areas; create area A, rack R-01, then optional location BIN-01 under the rack. Expected: hierarchy/path is readable, each list filters by its parent, and a rack can also be used directly later without a bin. Edit record names/descriptions/status without losing parent context.
6. Create A again under WH-01 and under a second warehouse. Repeat rack and bin code scenarios. Expected: same-parent duplicates rejected; same codes under different parents accepted. Parent IDs must refer to existing records; invalid Guid/unknown parent is rejected.
7. Deactivate an unused location, rack, area, warehouse, supplier and product; filter inactive and reactivate. Expected: history/records are retained; no hard-delete endpoint exists. Descendants may retain their own active flag but an inactive ancestor makes the whole placement unavailable for receiving. Reparenting existing areas/racks/locations is rejected to preserve historical placement identity; create a new placement instead.
8. With two browser sessions open on the same record, save in one then save the stale dialog in the other. Expected: 409 conflict and clear refresh instruction; the newer record is not overwritten. Reopen to obtain its current version.
9. Sign in as default WarehouseStaff (inventory.view). Expected: master data can be read; create/edit controls are absent and View dialogs are read-only. Direct POST/PUT attempts are forbidden. Grant only products.manage to a disposable staff role, refresh its session and verify product editing works while supplier/warehouse writes remain forbidden. Restore the role afterward.
10. Use an anonymous request and a signed-in request without valid CSRF against a write endpoint. Expected: 401 when unauthenticated; CSRF-protected mutations rejected; browser-supplied actor IDs cannot override audit identity.
11. Inspect Security audit after creates/edits/deactivation. Expected: MasterDataCreated/MasterDataUpdated events, actor from validated identity, UTC time, entity ID, safe before/after values and correlation ID. No credentials or supplier contact details in audit values.
12. At 375, 768, 1024 and 1440 widths, create/edit/view each master list and navigate warehouse > area > rack > optional position. Also check 320px and landscape. Expected: touch-friendly controls, responsive cards/table, associated labels, scrollable dialogs, working Escape/focus return, no unwanted page overflow. Loading/error/empty/success states and pagination must remain usable. Test >100 lookup records if available: selectors must load all pages, not silently truncate.
13. After Phase 4 is installed, receive stock at a location and attempt deactivating that position, its rack, area and warehouse. Expected: all are rejected while stock is held in the affected subtree, so existing stock remains accessible. Physical deletion remains unavailable even if currently empty.

## API and database inspection

All endpoints are under `/api/v1`: `suppliers`, `products`, `warehouses`, `areas`, `racks`, `locations`. Each has paged GET (`page`, `pageSize`, `search`, `active`, `parentId`), GET by Guid, POST, PUT by Guid. PUT requires the last returned base64 `rowVersion`. Do not send invented actor IDs or bypass the existing CSRF flow. See Development OpenAPI for exact schemas.

Read-only checks in DBeaver:

```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
SELECT Code, Name, IsActive FROM Suppliers ORDER BY Code;
SELECT Code AS SKU, Name, TrackExpiry FROM Products ORDER BY Code;
SELECT w.Code Warehouse, a.Code Area, r.Code Rack, l.Code Position
FROM Warehouses w
LEFT JOIN WarehouseAreas a ON a.WarehouseId=w.Id
LEFT JOIN StorageRacks r ON r.AreaId=a.Id
LEFT JOIN StorageLocations l ON l.RackId=r.Id
ORDER BY w.Code,a.Code,r.Code,l.Code;
SELECT TOP(30) Action, Entity, EntityId, ActorId, Timestamp, CorrelationId
FROM AuditLogs ORDER BY Timestamp DESC;
```

## Decisions, limitations and files

Codes/SKUs are uppercase identifiers; duplicate names are allowed. Location parentage is immutable after creation. Deactivation is used instead of physical deletion. External product ID is only an optional stable reference to the single legacy product identity, not an integration or variant implementation. Actual e-commerce mapping contract remains Phase 7. No sample/master seeding flag has been enabled. Audit values omit supplier contact PII. No automated tests were added. Owner manual acceptance has not been claimed.

Files: Shared/Operations (record base, validation, master configuration/service), modules Catalog/Product, Suppliers/Supplier, Warehouses/Entities, six master controllers, DbContext, central permissions/registration/handler, Program registration, Phase2MasterData migration/snapshot; frontend MasterData page, master-data types, useLookup, routes/sidebar/styles/permission labels; this document. Later phases extend occupied-location guards without rewriting Phase 2 schema history.
