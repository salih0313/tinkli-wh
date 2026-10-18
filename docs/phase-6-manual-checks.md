# Phase 6 — Completed physical stock movements

Owner acceptance pending. Follow Phase 5 setup and run commands below. No sample transactions are seeded.

```sh
dotnet restore Wros.slnx
dotnet tool restore
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet run --project src/Wros.Api --launch-profile https
# Another terminal:
cd src/wros-web
npm ci
npm run dev
```

Migration `Phase6CompletedStockMovements` adds completed transaction headers/selections and ledger references; existing receipts and balances remain unchanged. Use your existing HTTPS/SQL User Secrets, not LocalDB on Mac.

1. As SuperAdmin, post a DN with 100 units split across racks in warehouse A. Create active racks/positions in warehouse B. Note lot IDs and balances. Expected: physical 100 and matching receipt ledger.
2. Physically hand 8 units back to supplier, then open Supplier returns. Choose supplier and source placement, quantity 8; optionally enter original DN/PO UUID. Confirm physical completion and the confirmation dialog. Expected: SR-year-number, read-only details, source reduced by 8, signed SupplierReturn ledger, audit correlation. No original receipt edit.
3. Dispose 2 units physically. Record Damage & disposal with reason and exact lot/location. Expected: DS-year-number, source reduced by 2, immutable disposal audit/ledger. Expired physical stock may be disposed/returned.
4. Physically transfer 30 units from A to B, splitting 10/20 between destination racks using two selections from the same source. Record Warehouse transfers. Expected: WT-year-number, same lot/batch/expiry/receipt provenance, A down 30, B up 30; company total unchanged. Both signed transfer sides share reference number/ID.
5. Select source balance twice whose sum exceeds availability; also try zero, negative, fractional units, missing source, inactive destination, location under wrong rack and same-warehouse transfer. Expected: 400/409, no partial document, balance or ledger changes. Disposal requires reason; every operation requires confirmed physical completion. Backend repeats validation independently of browser.
6. For a successful submission, use browser Network tools to repeat exactly the POST with the same idempotencyKey/body/CSRF session. Expected: original document, no second deduction. Change quantity with that key: 409. After a network timeout keep the same form open and retry; do not create a new key until the outcome is known. Reusing a key across users is rejected.
7. In two sessions submit stock-outs whose combined quantity exceeds available source. Expected: at most valid available deductions succeed; other request returns 409 (including safe SQL deadlock conflict), never negative. Refresh before retry.
8. Create a staff role grant for outbound.manage only, then inventory.adjust only, then inventory.transfer only. Expected: only corresponding operation visible/allowed. Direct cross-operation POST/GET returns 403. Anonymous requests 401. Staff cookies continue requiring CSRF. No automated privilege changes are seeded.
9. After stock is consumed/transferred, attempt a DN rack correction moving the unavailable original units. Expected: 409; posted receipt totals/original allocations remain intact. DN corrections are not interwarehouse transfers.
10. At 375/768/1024/1440 pixels and both orientations, complete each form, destination split, confirmation and details; keyboard Tab/Escape. Expected: usable selectors/cards, visible validation, no clipping/overflow. Large stock source list is searchable/paged; selected source persists across pages.

## SQL checks (read only)

```sql
SELECT Id, Number, Kind, CompletedAt, ActorId, CorrelationId FROM StockTransaction ORDER BY CompletedAt DESC;
SELECT ReferenceType, ReferenceNumber, SUM(Quantity) AS NetUnits FROM StockMovements
WHERE ReferenceType='Transfer' GROUP BY ReferenceType, ReferenceNumber;
SELECT * FROM InventoryBalances WHERE Quantity<0;
SELECT Action, EntityId, CorrelationId FROM AuditLogs WHERE Action IN ('SupplierReturnPosted','DisposalPosted','TransferPosted');
```

Expected: transfer net zero, no negative balance, one document per idempotency key and relevant audit entries. Reconcile per-lot/location balances using Phase 5 queries.

Troubleshooting: refresh stock after 409; do not retry with changed quantities under an already committed key. 400 requires correction of selections/confirmation. 403 requires appropriate operation permission. Keep inactive products active until their remaining physical stock is dealt with. Do not repair balances through SQL.
