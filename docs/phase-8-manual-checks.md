# Phase 8 — Hardening and handover acceptance

Owner-operated acceptance remains pending. Builds and developer read-query inspections are reported separately in the delivery record. No automated test suite was added.

## Prerequisites and exact commands

Use .NET 10, Node22+, existing SQL Server database, existing local secrets and private seed file if users are absent. No business sample seed or production settings are populated automatically. Follow local-development.md and e-commerce-integration.md for sensitive configuration.

```sh
dotnet restore Wros.slnx --locked-mode
dotnet tool restore
dotnet build Wros.slnx -c Release --no-restore
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet ef migrations has-pending-model-changes --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet run --project src/Wros.Api --launch-profile https
# Another terminal
npm --prefix src/wros-web ci
npm --prefix src/wros-web run build
npm --prefix src/wros-web run dev
```

Expected: both builds succeed; database update is additive/idempotent; no pending model changes. Phase8 adds no further schema changes. Repeat database update and confirm users/passwords/stock/history unchanged, no duplicate seeds.

## Security and functional regression

1. Run existing Phase1/1.5 guides: valid admin/staff login, invalid/locked/inactive account, password change/reset, logout, fixed expiration and lost-session redirect. Expected: same secure staff cookie/CSRF architecture and minimum-eight-character-only password rule, no public registration.
2. Use roles-and-permissions.md to grant/revoke one permission at a time. Call each protected API directly, not only via menus. Expected: correct 401/403, no warehouse-specific restrictions, only SuperAdmin user/role management, exact stock operation grants. Confirm API mutation requests without CSRF fail even for admin.
3. Check each existing screen: Home, System, Users, Roles, Audit, Password, all six master lists/hierarchy paths, PO list/editor/detail/revision/print, DN list/editor/detail/post/correction, legacy Receipt stock. Expected: routes/imports/actions intact; original API contracts still available. Staff master read works only under existing reader grants.
4. Perform complete Phase5,6,7 guides, especially atomic negative cases, duplicate keys and simultaneous source deductions. Expected: no partial stock transactions, no negative balances, identical retry no extra ledger, correct immutable snapshot retrieval.
5. Send malformed UUID/page/pageSize, null/nested-null arrays, oversized bodies, fractional/zero/negative quantities and inactive hierarchy selections. Expected: bounded 400/413 or specific safe error; no stack/SQL/connection details. SQL injection-like search text is treated as parameterized text, never executed.
6. Browser DevTools: staff cookie Secure/HttpOnly/SameSite Strict and fixed expiry; no machine private key/password/token in browser storage. Direct machine endpoints reject staff cookies; assertions don't give staff access. Check wrong/replayed/expired assertions, scope and disabled clients.
7. Inspect correlation headers using a valid X-Correlation-ID UUID and invalid arbitrary text. Expected: valid UUID propagated; arbitrary input replaced by generated UUID. Errors/ledger/audit trace same ID. API has nosniff/no-store/no-referrer/frame protections; production has HSTS. Development OpenAPI available; diagnostics/OpenAPI absent outside Development. Do not deploy to test this; use isolated authorized local config.
8. Review Git changes/ignored files locally. Expected: no real credentials/production connections/private RSA keys in tracked files, seed example empty. Rotate any accounts that previously used example values; clearing the example doesn't rotate database passwords or rewrite Git history.
9. Stop local SQL temporarily only if it won't interrupt others. Expected: liveness remains healthy and readiness becomes unhealthy with safe response. Restore SQL; readiness recovers. Do not delete/reset SQL data.

## Database, performance and recovery

10. Run read-only reconciliation SQL in database-migrations.md and Phase7 guide. Expected: no negative balances, ledger equals balances per lot/location, transfer net0, unique event/order constraints and complete immutable snapshot row counts.
11. Use representative owner-provided data, not inserted fake balances. Observe inventory search, paging, dashboard, ledger, transaction histories and snapshot capture via browser Network and SQL query plans/STATISTICS IO in a separate read-only session. Expected: bounded pages (max100), projected inventory rows/SQL aggregations, bulk destination validation rather than per-line DB queries, indexed ledger timestamp/reference and integration event keys. Record actual latency/lock waits before tuning; sparse local inspection isn't a scale benchmark.
12. Capture snapshot while another session posts/deducts stock. Expected: immutable consistent committed capture or a safe failure/conflict that can be retried, never partial persisted snapshot. Stock operations use serializable transactions and rowversion; SQL deadlock409 does not produce partial effects. Snapshot application lock serializes capture versions.
13. Simulate interrupted snapshot retrieval and RTD timeout as Phase7. Expected: last good consumer inventory preserved, only complete increasing generation published, original business event retried. Review backup restore/source-generation watermark process with e-commerce developer before production.
14. Review production-readiness.md line by line. Expected: hosting, keys, retention, monitoring, real-volume profiling, role policy and legacy consumer success-gate remain explicitly owner-controlled. No production deployment happened.

## Responsive screen checklist

For every screen in step3 and new Inventory overview/explorer/expiry/ledger, Supplier returns, Damage & disposal, Warehouse transfers, Integration registry/mapping/history:

15. Check at 375,768,1024,1440px, portrait and landscape, and 320px minimum. Expected: no page horizontal overflow, mobile cards, collapsible sidebar/drawer, visible primary actions, readable UUIDs/correlation fields and no clipped controls.
16. Tab/Shift-Tab all filters/forms, Enter submit, Escape confirmation; open/close drawer and account menu. Expected: associated labels, visible focus, modal focus containment/restoration, keyboard-reachable actions and minimum44px important targets.
17. Use slow/offline network, empty queries, backend validation failure and successful operations. Expected: loading/empty/error/success feedback, no stale repeated stock submission, form data preserved on errors and operation references available after success.
18. In inventory product grouping expand one item, inspect Network. Expected: details load on expansion and page independently; closed groups don't eagerly fetch every product. Search is debounced. Filter warehouse/area/rack selectors; expected coherent hierarchy choices.

## Troubleshooting

Use troubleshooting.md. For failures record URL, safe correlation ID, time and expected/actual outcome without secrets. Re-run affected checks after fixes; owner acceptance is required before production planning. Do not call these checks passed based only on successful builds.
