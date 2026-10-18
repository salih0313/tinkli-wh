# Troubleshooting

| Symptom | Checks / action |
|---|---|
| LocalDB platform error on Mac | Replace LocalDB with existing SQL Server TCP User Secrets connection; see local setup. LocalDB is Windows-only. |
| `database update` says Done but wrong database viewed | Pass `-- --environment Development`, check configured server/database privately, reconnect DBeaver host localhost port1433 and database from your own connection. Do not paste connection passwords. Query __EFMigrationsHistory. |
| Seed password error on a new database | Fill ignored DevelopmentSeedCredentials.local.cs, at least 8 chars, rebuild and rerun update in Development. No WROS_SEED flag. Existing users are never reset by editing seed file. |
| Existing seed password does not log in | Password may have been changed after seed; use approved SuperAdmin password reset. Editing seed file won't reset it. Unknown/inactive/locked accounts have the same safe login error. |
| Browser Failed to fetch | API running at7243? Vite at5173? Trust dev certificate; open API readiness directly. Check Vite proxy. Vite frontend is http locally while proxy uses HTTPS backend. |
| API liveness healthy, readiness unhealthy | Check SQL running, TCP port, private connection, login permissions, migrations; health response deliberately omits credentials/provider details. |
| 401 staff | Login/session expired/security stamp changed/disabled account; sign in again. Fixed session has no silent refresh extension. |
| 403 staff | SuperAdmin verifies actual DB role grants; refresh displayed frontend session. User/role administration additionally requires SuperAdmin. |
| 400 CSRF | Refresh page/session; staff POST/PUT need same-origin secure cookie and X-CSRF-TOKEN. Don't bypass validation. |
| 409 stock/document | Another operator changed version/availability or a deadlock was safely rolled back; refresh before retry. Never force stale stock updates. |
| Inventory empty | Drafts/POs add no stock. Post verified allocated DN. Clear hierarchy/batch/expiry/search filters. |
| Physical exceeds sellable | Expired/inactive product remains physical; sellable excludes it. Expiry today is valid through UTC day. No checkout reservations are represented. |
| Location cannot deactivate | Positive stock exists under its hierarchy; physically handle it first and record legitimate movement. |
| DN correction rejects consumed stock | Original allocation anchors full receipt quantity; moved/consumed units aren't available to rewrite. Use completed interwarehouse transfer for actual warehouse movement. |
| Integration401 | Fresh assertion each request, valid RS256/kid/type/audience/issuer/subject/time, active registry+allowed public key; synchronize clocks. Never reuse a jti even after validation error. |
| Integration403 | Grant only required server-configured machine scope; staff SuperAdmin cannot substitute for a machine identity. |
| Integration422 | Exact stable mapping and selected product/lot/location IDs; check expiry. Do not match names. |
| Integration409 | Inspect event/order history and correlation before changing anything. Identical retry returns prior result; conflicting event body/order identity is blocked. |
| Snapshot410 | Discard staging, keep old published inventory, capture anew; never combine IDs. |
| Snapshot413 | Row capacity exceeded; no truncated data. Review capacity/storage/locking before increasing config. |
| 429 | Wait/backoff, don't loop rapidly; machine retry uses fresh assertion but original business event. |
| Network timeout at RTD | Outcome unknown; retry exact original event/body with new assertion until confirmed. E-commerce remains pending. |
| Unexpected error | Capture safe correlation ID and UTC time; look at structured logs by CorrelationId. Don't expose bodies/tokens/connection strings in support tickets. |

Read-only SQL reconciliation is in database-migrations.md. No direct SQL balance edits, deletion of movements, automatic database reset or blind generation of new event IDs is an acceptable repair path.
