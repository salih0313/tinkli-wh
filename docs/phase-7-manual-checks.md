# Phase 7 — Integration acceptance

Manual acceptance is pending; these are owner-operated instructions. Never point the examples at production.

## Prerequisites and startup

Use .NET 10, Node 22+, SQL Server, existing User Secrets and posted stock with known lot/location IDs. Follow [e-commerce integration](e-commerce-integration.md) to generate a private caller key outside Git, register an active client, configure its **public** key/allowlist/scopes and create explicit product mappings. No integration clients, keys, mappings or transactions are seeded automatically.

```sh
dotnet restore Wros.slnx
dotnet tool restore
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet run --project src/Wros.Api --launch-profile https
# Another terminal
cd src/wros-web
npm ci
npm run dev
```

`Phase7Integration` adds registry/mappings, assertion replay keys, immutable snapshots and external deductions/lines, plus ledger client identity. Existing data is preserved. Use the exact shell setup, signing utility and curl examples in the integration guide. The caller requires a fresh assertion for **each** HTTP request.

## Numbered checks

1. Anonymous request to POST integration snapshots; repeat using only a valid staff cookie. Expected: 401, no snapshot. Request a staff management endpoint with only a machine assertion. Expected: 401/403, no staff identity/role elevation.
2. Sign an assertion with unregistered key, wrong kid/issuer/subject/audience/type, HS256/unsigned instead of RS256, expired or >120-second lifetime. Expected: 401 and safe correlation error; no body/private key/token in logs. HTTP is redirected to HTTPS; callers must originate HTTPS and never follow a redirect carrying credentials to another host.
3. With a valid assertion, create snapshot. Reuse **that same assertion** for any request. Expected: replay rejected 401. Generate a new assertion and retry the snapshot page. Expected: success. Remove orders.deduct scope and attempt deduction. Expected: 403. Disable registry client and retry valid assertion: 401.
4. Configure a second public key/new kid while retaining old key; restart API, sign with new key. Expected: succeeds. Remove old key/restart; old signatures fail. Never commit keys or real assertions.
5. Create active product mapping. Duplicate same client/external ID: 409. Update mapping using stale rowVersion: 409. Disable mapping, attempt deduction: 422. Restore mapping. Different external variants may map to one internal product; they reference shared stock, not independent inventory.
6. Create a snapshot from several lots, including expired and undated ones. Fetch pages with pageSize=1 and a fresh assertion per page. Expected: contiguous ordinals, fixed ID/version/header, physical/sellable/source DN/location/lot fields, mapped external IDs and FIFO/FEFO ranks. Expired/inactive sellable zero/ranks null; valid earliest receipts FIFO, valid nearest expiry FEFO, undated last in FEFO.
7. Between page requests, post/transfer/dispose real stock through WROS. Expected: old snapshot's next pages remain unchanged; a newly created snapshot reflects committed stock and has higher numeric version. SHA256 over each exact payload string+LF matches contentHash, count/ordinals complete. Empty inventory snapshot has 0 rows and SHA256(empty).
8. Interrupt retrieval halfway. Expected consumer behavior: keep last published inventory; retry same ID's missing page or discard staging. Simulate expiry by configuring SnapshotMinutes=1, restarting, creating snapshot and waiting >1 minute. Expected: 410, discard staging/restart. Never merge pages from different IDs. Attempt to publish an older version after newer: consumer must reject it atomically. WROS cannot enforce writes inside the unchanged legacy application; verify its consumer implementation before production.
9. Physically pick 8 units from two lots/racks (3+5). Submit the documented Ready-to-Deliver body with all stable IDs, exact active mapping, one line quantity 8, stable event/order/line IDs. Expected: one atomic success `Deducted`; both balances down, original receipt unchanged, two ExternalOrder ledger rows, machine client identified, audit has order/event/correlation.
10. Retry the identical business body/event key with a **fresh assertion**. Expected: exact original result (ID/time/correlation), no extra movements/deduction. Change quantity under same event: 409. New event for same already deducted external order: 409. Different client cannot retrieve another client's snapshot.
11. Submit a new event containing one valid selection and one insufficient/expired/wrong-product/wrong-warehouse/wrong-position selection. Expected: 400/409/422, **no** partial stock changes, deduction record or ledger. Try sum mismatch, zero/negative/fractional quantities, missing/null selections and duplicate external line IDs: validation error. Correct and resubmit only when no successful outcome exists.
12. Two callers simultaneously deduct more than the same balance can cover. Expected: no negative stock; one succeeds where available and the other gets 409/conflict. Repeat the losing valid event after refresh only if stock still sufficient. Retry a successful event after a later stock change: original success, no new deduction.
13. Model network timeout after commit by ignoring the first success response and retrying its exact body/new assertion. Expected: original result. E-commerce must persist pending event and finalize RTD only after confirmed WROS success; missing/failed events appear in reconciliation, not silently finalized orders.
14. As staff without integration.manage, open integration UI/direct management API. Expected: menu hidden/403. Grant it explicitly and refresh; registry/mappings/history accessible, no credentials exposed. At 375/768/1024/1440 widths and both orientations, edit client/mapping with keyboard; expected readable cards, labels, focus, pagination, no page overflow.

## SQL verification (read only)

```sql
SELECT Id, Name, IsActive FROM IntegrationClient;
SELECT ClientId, ExternalProductId, ProductId, IsActive FROM ProductMapping;
SELECT Id, Version, ClientId, CreatedAt, ExpiresAt, TotalRows, ContentHash FROM InventorySyncSnapshot;
SELECT SnapshotId, COUNT(*) AS Rows, MIN(Ordinal) AS FirstRow, MAX(Ordinal) AS LastRow
FROM InventorySyncSnapshotLine GROUP BY SnapshotId;
SELECT ClientId, EventId, ExternalOrderId, COUNT(*) AS Copies FROM ExternalOrderDeduction
GROUP BY ClientId, EventId, ExternalOrderId;
SELECT ReferenceId, IntegrationClientId, Quantity, CorrelationId FROM StockMovements WHERE Type='ExternalOrder';
SELECT * FROM InventoryBalances WHERE Quantity<0;
```

Expected: unique event/order per client, complete snapshots, immutable signed deductions, no negative balances. Use Phase 5 ledger reconciliation query. Never inspect/paste stored credentials; replay table stores jti hashes, not assertions.

## Troubleshooting

401: fresh assertion, UTC clock, exact HTTPS audience/issuer/subject/kid/type, active registry and configured allowlist/public key. 403: scope/permission. 422: mapping or selection mismatch/expired lot. 409: inspect prior event/order and refresh stock; retry identical event for unknown outcomes. 410: restart full snapshot. 429: backoff/new assertion. Share only correlation ID with support. Server restart is required after key configuration changes. Private key remains with caller; browser staff UI cannot sign machine calls.
