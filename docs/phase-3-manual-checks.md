# Phase 3 — Purchase orders manual acceptance

## Prerequisites and commands

Phase 2 migration and master data must exist. Keep the existing database and credentials. Start SQL Server/Docker. Use a SuperAdmin or deliberately grant purchaseorders.manage to a staff role; default WarehouseStaff retains its existing inventory.view-only mapping.

From the repository root:

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

Migration: `Phase3Purchasing`. It adds DocumentCounters, PurchaseOrders, PurchaseOrderLines and PurchaseOrderRevisions. No demo documents or stock are seeded. No new roles, permissions, credentials or config are required. Number format is PO-YYYY-000001, counted separately per UTC creation year; numbering does not use a backdated document date. Counter increments and PO creation share a transaction. Historical numbers are stable. Owner selected whole units and manual supplier sharing.

## Numbered checks and expected results

1. As SuperAdmin, create an active supplier and two active products if needed, then open Purchase orders. Expected: permission-aware nav, paginated list, clear empty/loading/error states. Users without purchaseorders.manage cannot write or use the purchasing workflow.
2. Create a PO with supplier, date, optional delivery date/remarks, and a product quantity of 10. Add/remove a second line. Save. Expected: server-generated unique PO-YYYY-NNNNNN, Draft status, Guid identities independent of business number, revision 1, ordered lines and created/updated actor/UTC timestamps. No approval is requested and no stock/balance is created.
3. Submit missing supplier, unknown product, inactive supplier/product, zero, negative and fractional quantities; try a blank date and excessively long remarks. Expected: 400/clear field or API error; no partial document, counter allocation or revision commits. Only positive whole-unit quantities are valid. Client/server integers are limited to the safe JSON integer range (9,007,199,254,740,991), with SQL decimal(18,0) storage.
4. Choose Record send; cancel its confirmation, then repeat and confirm. Expected: cancel has no effect; confirmation marks Sent and creates a new Sent revision/audit event. UI clearly tells staff to print/download and share manually. No email or other external message is transmitted.
5. Edit the sent PO, change 10 to 12 and add a product, save, then Record resend. Expected: editing is allowed until receiving starts, with separate Edited and Resent snapshots; the prior lines/remarks/supplier identity remain inspectable in history. Status remains Sent after editing and staff deliberately record the resend; no automatic supplier communication or backorder appears.
6. Open every revision entry. Expected: revision numbers increase, action, actor and UTC timestamps are present, and formatted snapshots preserve the business number, supplier label/ID, product IDs/SKUs/names, dates, quantities, remarks and state at that point. Supplier contact PII and credentials are not copied into snapshots.
7. Choose Print / Save PDF. Expected: stale-version check and a PrintPrepared audit event precede the browser print dialog; sidebar/header/actions are absent from print preview, product lines remain legible, remarks wrap, and table headers repeat across pages. Use the browser's Save as PDF destination to download. This is browser PDF support, not an email or external PDF service; canceling print does not claim a document was physically printed.
8. Search by number or supplier and filter Draft/Sent/Locked; refresh and use pagination. Expected: stable ordering and correct page bounds. Browser Back/Forward and direct `/purchase-orders/:id` and `/:id/edit` URLs work; unknown IDs show a safe error.
9. Open the same PO in two sessions. Save or record send in one, then submit the other's older rowVersion. Expected: 409 conflict, no stale overwrite or duplicate revision. Reload before retrying. Concurrent creates must receive distinct business numbers; a database lock conflict returns a retriable 409 rather than silently duplicating a number.
10. After Phase 4 is installed, create a DN linked to this PO, even while it is a draft DN. Expected: the PO becomes Locked atomically with DN creation; edit and send/resend are rejected by the server. Creating a second linked DN is allowed. There is no cancel/delete/unlock workflow, so draft-DN cancellation cannot accidentally unlock history. Print/read/revision history remain available.
11. Create and send another PO without creating a DN. Expected: it remains editable. Neither saved, sent, resent nor locked POs add any stock. No requirement ties requested quantity to actual receipts.
12. Test anonymous requests, absent permission, and missing/invalid CSRF for mutations. Expected: 401/403 or CSRF 400 as appropriate; backend policy enforcement independent of hidden menus. The actor is taken from validated claims, never the payload. Restore any temporary role grants after testing.
13. Inspect audit. Expected: PurchaseOrderCreated/Edited/Sent/Resent/PrintPrepared events, document reference, revision, validated actor and UTC correlation. In Phase 4, ReceivingStarted also records the lock. Existing Identity and master-data screens still work.
14. At 375, 768, 1024 and 1440 widths, create/edit/read a PO with long product names, multiple lines and remarks, inspect history and print preview. Also rotate a mobile device and try 320px. Expected: cards for narrow lists/lines, no unwanted horizontal page overflow, sticky form actions reachable, numeric mobile keyboard, associated labels, visible focus, confirmation Escape/cancel, useful loading/error/success feedback. Remove/add lines without losing other values.

## APIs and read-only SQL verification

Base `/api/v1/purchase-orders`: GET paged list (`search`,`status`,`page`,`pageSize`), GET `/{id}`, GET `/{id}/revisions`, POST create, PUT `/{id}`, POST `/{id}/send`, POST `/{id}/prepare-print`. Version-sensitive operations require the last returned `rowVersion`. Use Development OpenAPI and existing CSRF flow.

```sql
SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;
SELECT Number, Status, Revision, SupplierId, OrderDate, CreatedAt, UpdatedAt
FROM PurchaseOrders ORDER BY CreatedAt DESC;
SELECT po.Number, l.Position, p.Code SKU, l.Quantity
FROM PurchaseOrderLines l JOIN PurchaseOrders po ON po.Id=l.PurchaseOrderId
JOIN Products p ON p.Id=l.ProductId ORDER BY po.Number,l.Position;
SELECT po.Number, r.Revision, r.Action, r.Timestamp, r.ActorId
FROM PurchaseOrderRevisions r JOIN PurchaseOrders po ON po.Id=r.PurchaseOrderId
ORDER BY po.Number,r.Revision;
SELECT [Key],LastNumber FROM DocumentCounters ORDER BY [Key];
```

Never alter counters, snapshots or schema with manual SQL. These are inspection queries only.

## Limitations and changed files

No prices, currency, taxes, variants, product UOM conversion, approvals, cancellation or mandatory backorder workflow is invented. Quantities are units. Send/resend records a business event for manual sharing, as approved; supplier delivery is not asserted. Download uses browser Save as PDF. Full inventory screens remain Phase 5; a PO itself never affects stock. No automated tests added; owner manual tests have not been claimed as passed.

Files: Modules/Purchasing (entities, configuration, contracts, service), Shared/Operations/DocumentNumbers, PurchaseOrdersController, DbContext and Program registrations, Phase3Purchasing migration/snapshot; frontend purchasing types, PurchaseOrders, PurchaseOrderEditor, PurchaseOrderDetails, routes/navigation and document/print styles; this document. Phase 4 adds the linked-DN lock and read access for receiving lookups.
