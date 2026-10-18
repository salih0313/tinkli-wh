# Phase 4 — Receiving and stock-in manual acceptance

## Scope and prerequisites

Phases 2 and 3 must be installed. Use the existing SQL Server database and Identity accounts; never recreate/reset the database. A SuperAdmin can run all checks. To check staff permissions, deliberately grant receiving.manage to a disposable staff role and optionally inventory.view for the read-only Receipt stock screen. Existing role mappings are not silently expanded.

Owner decisions: whole-unit quantities; yearly PO/DN numbers; manually shared supplier documents. No product variants, prices, tax/UOM conversion, general transfer, stock-out, checkout reservation, automatic lot selection or e-commerce integration is implemented.

Receiving includes the minimum lot/balance/ledger structures and a basic receipt stock view. General inventory searching, FIFO/FEFO, expiry alerts and reporting remain Phase 5. Corrections are limited to rack/location reassignment within each warehouse's existing received total; interwarehouse transfers remain Phase 6.

## Migration and run commands

From repository root, with SQL Server running:

```sh
dotnet restore Wros.slnx
dotnet tool restore
dotnet build Wros.slnx
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet ef migrations list --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet run --project src/Wros.Api --launch-profile https
```

Second terminal:

```sh
npm ci --prefix src/wros-web
npm run dev --prefix src/wros-web
```

Open http://localhost:5173. Build frontend: `npm run build --prefix src/wros-web`.

Migration `Phase4Receiving` adds DeliveryNotes, DeliveryNoteLines, StockLots, DeliveryNoteAllocations, LotPlacements, InventoryBalances and StockMovements. It retains all prior migrations, counters, accounts and master data. There are no new settings, secrets, roles or seed transactions. No artificial balances are inserted. Existing local seed credentials remain private and existing account passwords are not reset.

## Important document semantics

- Numbers use DN-YYYY-NNNNNN with a separate counter for each UTC creation year. Physical received timestamps are entered in local time and stored in UTC; the number's year follows creation time rather than backdating.
- PO lines are an editable starting point, never a stock source. A linked DN must have the PO's supplier; substitutes and unexpected products can differ from PO lines.
- Saving the first linked draft DN atomically locks the PO and adds a ReceivingStarted revision. Multiple DNs may reference it. No cancel/delete/unlock workflow exists, so a draft DN cannot accidentally unlock its PO.
- Drafts can have incomplete or unmatched lot allocations and zero received quantities. Saving a draft creates no balance or ledger movement. Internal draft lot IDs can change on a draft rewrite; posted lot IDs remain stable.
- Posting requires each line's lots to sum to its actual quantity and each lot's positive allocation entries to sum to its quantity. An all-zero receipt creates no stock movements. A tracked product requires expiry on positive lots; batch and expiry are otherwise independently optional.
- Rack-only placements are supported; bins/drawers/trays are optional positions under a rack. Destinations are grouped by warehouse and checked against active ancestors. Repeated rack/position entries within one lot must be combined.
- Posted receipt lines, lot quantities, batches, expiry and original allocation rows cannot be edited through the application. Corrections change separate current nominal assignments and append signed relocation entries. They never rewrite original receipt movements.
- The correction editor preserves the **full originally received** quantity. A consumed portion remains assigned to its old position; negative deltas are limited to actual current source balance. This supports partial remaining-stock corrections without pretending consumed units can move.
- Positive relocation destinations must be active. An unchanged nominal assignment anchoring already-consumed stock can remain at an inactive empty position. Current physical balances remain separate from nominal received assignments.
- Expiry is compared with the current UTC calendar date. A lot is expired when its expiry date is earlier than today; expiry today remains sellable for that date. Expired stock is preserved physically with sellable quantity zero. Inactive products also report zero sellable quantity.
- All quantity inputs must be whole units in the safe JSON integer range; SQL quantity precision is decimal(18,0). Resource limits: 500 lines, 100 lots per line, 500 allocation entries per lot, 5,000 allocation entries per document/correction, and a 2MB receiving payload limit. These are API capacity limits, not unit conversions.

## Numbered manual scenarios and expected results

1. **Baseline and permissions.** Sign in as SuperAdmin. Create active supplier S-RECEIVE, products A and B, and WH-RECEIVE > Area A > three racks R1/R2/R3. Create a bin under one rack. Record starting balances. Expected: no stock from master creation. Default inventory.view-only staff can see Receipt stock/master references, not receiving/purchasing actions. receiving.manage grants receiving only, not product/supplier/user administration or general stock adjustment.
2. **PO 10, actual 8.** Create/send a PO for product A quantity 10. Choose Receive from this PO or pick it on a new delivery note. Change actual quantity to 8. Expected: copied lines are editable; no reason per line is required; one general remarks field is available. The single lot quantity follows a single-lot line quantity edit. Saving a draft locks the PO but adds no stock. PO still says requested 10; DN says actual 8. No backorder is created.
3. **Substitutes and extra products.** In the draft, replace one product, add B, remove an unreceived line, add a product created through the Products screen in a new tab, then Refresh choices. Expected: permission-authorized new products become selectable; receiving-only staff cannot create products without products.manage. Products match stable IDs/SKUs, not names. Supplier must match linked PO; unknown/inactive product or supplier is rejected.
4. **Standalone receipt.** Create a DN with no PO. Enter supplier reference, received timestamp and remarks. Expected: independent DN number, optional PO/ref, no PO fabricated, no quantities/stock generated until posted.
5. **Multiple receipts per PO.** Create a second linked DN with fewer, more or different products. Expected: allowed; PO remains locked, original PO lines do not change, neither receipt is forced to equal ordered quantities. Draft receiving locks PO editing and send/resend even if the PO had not yet been marked Sent.
6. **Lot and expiry entry.** For product A with Track expiry enabled, create a positive lot without batch/expiry and save draft. Expected: draft can remain incomplete, but posting fails with an expiry error. Add expiry without batch and retry; valid. For an untracked product, verify batch-only, expiry-only and neither are allowed. Create two positive lots with identical supplier batch codes. Expected: separate unique internal lot IDs and no silent merge.
7. **Split 100 across racks.** Set a line/lot to 100 units. Allocate R1=40, R2=35, R3=25; include optional bin placement in a separate scenario. Expected: allocated 100/100, remaining 0. Save draft; no balance or movement yet. A rack-only destination is valid without requiring a bin.
8. **Allocation errors.** Try totals 99 and 101, negative/fractional quantities, zero allocation entries, a duplicate rack/position in the same lot, nonexistent rack, location from a different rack, inactive location and inactive ancestor. Expected: validation/400 or constraint conflict, no stock changes or partial writes. Zero received/lot quantities are allowed with no allocations. Draft total mismatches can be saved but cannot post.
9. **Post confirmation.** Open the correctly allocated draft, choose Verify & post stock, cancel, then confirm. Expected: cancellation has no effect; confirmation atomically marks Posted, records actor/time, creates lot/location balances, original Receipt movements and current nominal placements. All 100 units are immediately physically available. Posted receipt quantities/batches/expiry cannot be edited.
10. **Duplicate posting and concurrency.** Retain the draft rowVersion and submit the same post twice (or use two sessions). Expected: one posting succeeds; the other returns 409/stale/already-posted conflict. Original Receipt movements are not duplicated; balances remain 100. A timeout after commit is resolved by refreshing the DN, not assuming failure and creating a second receipt. Two simultaneous edits/corrections must not overwrite each other; retry only after refreshing current state.
11. **Atomic failure.** Make a multi-line draft where one line is valid and a later one has an allocation mismatch or missing required expiry. Attempt posting. Expected: entire request fails; no line receives stock and no movement/counter/posted status is partially committed. Correct it and post once.
12. **Expired stock.** Post an otherwise valid tracked lot with expiry yesterday. Expected: physical on-hand retains the quantity; sellable quantity is zero in DN details and Receipt stock. Expiry today is not yet expired under the documented UTC-date rule. A lot without expiry is sellable unless the product is inactive. Change a stocked product to inactive and refresh: physical stays, sellable becomes zero; restore its status afterward.
13. **Posted rack correction.** For the 100-unit receipt at 40/35/25, physically relocate 10 units R1→R2 within the same warehouse, then edit nominal allocations to 30/45/25. Cancel confirmation once, then repeat/confirm. Expected: cancellation has no effect; confirmation preserves total 100, lot identity, original DN allocations 40/35/25 and original Receipt ledger. Current balances become 30/45/25. New RelocationOut -10 and RelocationIn +10 share correction and correlation identifiers; audit stores safe before/after nominal assignments.
14. **Correction errors.** Try changing a lot's total, introducing fractions/negative entries, foreign DN lot IDs, stale rowVersion, duplicate positions, or an inactive positive destination. Expected: rejection without partial changes. Try moving the net lot quantity to another warehouse: rejected because Phase 4 rack corrections preserve each warehouse total. Initial receiving may allocate a lot across warehouses; subsequent corrections can redistribute racks within each one.
15. **Unavailable/consumed source.** The service checks each negative delta against actual source balance and will reject a move of consumed units. Full end-to-end consumption testing requires a real stock-out workflow in a later phase; none is added as a test backdoor. When available, consume part of a receipt, leave the consumed nominal portion assigned to its source, and move only remaining units. Expected: sufficient remaining units can move; exceeding current source balance returns 409 and rolls back every change. Do not manually overwrite balances to simulate consumption, because that would break the ledger.
16. **Occupied hierarchy.** Attempt to deactivate a stocked bin, rack, area and warehouse. Expected: 409 for every occupied subtree. Empty placements may be deactivated; no physical deletion is offered. After a valid rack correction empties a source rack, that unused rack can be deactivated. Historical original receipt allocations/movements remain readable.
17. **Read-only stock and history.** As inventory.view staff, visit Receipt stock. Expected: stable paged physical balances by product, warehouse, path, unique lot, optional batch/expiry, received date and sellable quantity; no stock-edit/stock-add buttons. As receiving.manage staff, inspect the DN's paged receipt/relocation history, actor IDs, UTC timestamps, references and signed movement quantities. No FIFO/FEFO, broad inventory search, transfer or stock-out module appears.
18. **Security.** Try anonymous requests, staff without receiving.manage, missing/invalid CSRF on mutations and frontend-supplied actor IDs. Expected: existing 401/403/CSRF protection; endpoint permission checks apply independently of menus, identity comes only from validated claims. No public registration or new auth mechanism. Sensitive receiving operations retain per-user throttling.
19. **Audit.** Inspect DeliveryNoteCreated/DraftUpdated/Posted/AllocationCorrected, PO ReceivingStarted and master deactivation events. Expected: validated actor, entity reference, UTC time, safe values/correlation; correction references join ledger rows. Passwords, tokens and supplier contact PII are absent from audit payloads. Ledger and PO revision updates/deletes are guarded as append-only in application persistence.
20. **Mobile and keyboard.** At 375, 768, 1024, 1440 (also 320 and landscape), use each DN screen: listing/filter, new standalone, PO-copy, edit, lot entry, split allocation, details, post confirmation, rack correction, stock and movement pagination. Expected: readable cards, correct allocation/remaining totals, touch-friendly controls, associated labels, visible focus, useful skeleton/error/empty/success states, scrollable dialogs, Escape closing only the top confirmation and no unwanted horizontal page overflow. Check real mobile numeric/date/time keyboards, long names/IDs, browser zoom and screen reader. Refresh choices must preserve typed draft values.
21. **Regression.** Repeat Phase 1.5 login/logout/user/roles/audit/password/system checks, Phase 2 master checks and Phase 3 PO print/PDF/history checks. Expected: approved functionality still works and only implemented operational links appear. No legacy e-commerce application/source/database is changed.

## API map

All receiving actions require receiving.manage under `/api/v1/delivery-notes`:

- GET list (`page`,`pageSize`,`search`,`status`); GET `/{id}`.
- GET `/purchase-orders` for lightweight header lookup; GET `/placements` for active rack/optional-position choices, both paged.
- POST create; PUT `/{id}` draft replacement.
- POST `/{id}/post` with rowVersion; POST `/{id}/correct-allocations` with rowVersion and lot allocations.
- GET `/{id}/movements` paged receipt/relocation history.

GET `/api/v1/receipt-stock` requires inventory.view; optional deliveryNoteId filters a receipt. GET purchase-order references/history is allowed to purchasing or receiving users; purchase-order create/edit/send/print preparation still require purchaseorders.manage. Exact schemas are available in Development OpenAPI. No new Identity mechanism or client actor field is introduced.

## Read-only database verification

Run in the existing database through DBeaver. All queries below inspect data only.

```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
SELECT Number, Status, PurchaseOrderId, ReceivedAt, PostedAt, PostedBy FROM DeliveryNotes;
SELECT Number, Status, Revision FROM PurchaseOrders;
SELECT sl.Id LotId, p.Code SKU, sl.SupplierBatch, sl.Expiry, sl.Quantity, sl.ReceivedAt
FROM StockLots sl JOIN Products p ON p.Id=sl.ProductId;
SELECT StockLotId, WarehouseId, RackId, LocationId, Quantity FROM InventoryBalances;
SELECT DeliveryNoteId,StockLotId,Type,Quantity,Timestamp,ActorId,CorrelationId,CorrectionId
FROM StockMovements ORDER BY Timestamp,Id;

-- Expected: no negative balances.
SELECT * FROM InventoryBalances WHERE Quantity<0;

-- Expected: no non-zero relocation group, globally or per warehouse.
SELECT CorrectionId,WarehouseId,SUM(Quantity) NetUnits
FROM StockMovements WHERE CorrectionId IS NOT NULL
GROUP BY CorrectionId,WarehouseId HAVING SUM(Quantity)<>0;

-- Expected: no ledger/balance discrepancies, including rack-only NULL positions.
WITH Ledger AS (
  SELECT StockLotId,RackId,LocationId,SUM(Quantity) Quantity
  FROM StockMovements GROUP BY StockLotId,RackId,LocationId
)
SELECT COALESCE(l.StockLotId,b.StockLotId) LotId,l.Quantity LedgerQty,b.Quantity BalanceQty
FROM Ledger l FULL JOIN InventoryBalances b
 ON b.StockLotId=l.StockLotId AND b.RackId=l.RackId
 AND (b.LocationId=l.LocationId OR (b.LocationId IS NULL AND l.LocationId IS NULL))
WHERE COALESCE(l.Quantity,0)<>COALESCE(b.Quantity,0);

-- Until a later stock-out phase, expected: posted lot quantities equal physical totals.
SELECT sl.Id,sl.Quantity,COALESCE(SUM(b.Quantity),0) PhysicalUnits
FROM StockLots sl
JOIN DeliveryNoteLines dl ON dl.Id=sl.DeliveryNoteLineId
JOIN DeliveryNotes dn ON dn.Id=dl.DeliveryNoteId
LEFT JOIN InventoryBalances b ON b.StockLotId=sl.Id
WHERE dn.Status='Posted'
GROUP BY sl.Id,sl.Quantity HAVING sl.Quantity<>COALESCE(SUM(b.Quantity),0);

-- Original allocations remain separate from current nominal assignment.
SELECT StockLotId,RackId,LocationId,Quantity FROM DeliveryNoteAllocations;
SELECT StockLotId,RackId,LocationId,Quantity FROM LotPlacements;
SELECT TOP(50) Action,Entity,EntityId,ActorId,Timestamp,CorrelationId
FROM AuditLogs ORDER BY Timestamp DESC;
```

## Files and limits

Backend: Modules/Receiving entities/configuration/contracts/queries/draft and posting services, Modules/Inventory entities, DeliveryNotes/ReceiptStock controllers; DbContext and Program registrations; AuditService correlation accessor; master occupied-location guard; PO ReceivingStarted lock and receiving read policy; Phase4Receiving migration/snapshot. Frontend: receiving types and AllocationEditor, DeliveryNotes/Editor/Details, ReceiptCorrection and ReceiptStock pages, lookup refresh, PO receive action, routes/sidebar/home/styles. Also this document and the phase delivery guide.

No automated tests were created. Build/migration verification is separate from owner manual acceptance; no owner manual test is claimed passed. Database administrators can bypass application permissions, so production least-privilege accounts remain an operational deployment responsibility. Consumed-stock end-to-end checks await a genuine later stock-out workflow. Browser PDF output, real-device keyboards and assistive technology require owner verification. Production seeding/hosting and all Phase 5+ modules remain outside scope.

**Stop after Phase 4. Owner manual verification and explicit approval are required before Phase 5.**
