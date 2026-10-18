# Production readiness checklist — deployment not performed

Implementation/build verification does not imply operational approval. Complete owner manual acceptance and a separate deployment authorization before production. No legacy e-commerce code/database has been modified.

## Required decisions and controls

- [ ] Owner accepts Phases 5–8 manual guides including concurrent source consumption and ledger reconciliation on representative data.
- [ ] Final staff role mappings approved; least privilege reviewed. Initial demo WarehouseStaff retains inventory.view only. Separate service client scopes audited.
- [ ] Rotate accounts if they used the former tracked example seed values. Removed example values must not be reused; Git history removal/rotation is an owner decision, not automatically rewritten here.
- [ ] Development local credentials excluded from build/deployment artifacts/source control; no demo seeding outside Development. Provision production SuperAdmin through an approved secure process; no production default account.
- [ ] HTTPS trusted application certificate, SQL trusted encryption, explicit AllowedHosts/CORS, least-privilege SQL account. Never use SQL sa for production.
- [ ] Same-origin HTTPS SPA/API hosting configured by a separately authorized deployment task. Vite is development-only. No untrusted forwarded-header middleware; when behind a proxy configure known proxies/networks and HTTPS forwarding before enabling machine calls.
- [ ] Persistent protected ASP.NET Data Protection key ring for staff sessions; file ACLs/backup/rotation plan. Current local key handling is not a production hosting design.
- [ ] Database backup and restore rehearsed; additive migration SQL reviewed against a copy of production. No automatic production migrations/resets. Avoid downgrade after new ledger types are used.
- [ ] Caller private RSA keys in its secret vault, WROS public keys/scopes in reviewed configuration, rotation/revocation ownership and synchronized clocks established. Separate clients for least-privilege read/deduct access where practical.
- [ ] Legacy developer implements the documented consumer contract: durable pending RTD event, exact selected IDs, WROS-success gate, same-event retry, conflict reconciliation, stable product mapping, atomic complete snapshot publication and increasing version watermark.
- [ ] Owner approves how external product/variant identities share one internal physical product; consumer never multiplies stock across variant mappings or overwrites checkout reservations.
- [ ] Snapshot capacity/lifetime reviewed on real volume (defaults 100,000 rows / 30 minutes). Captures use serializable locks and can block writes briefly; measure time/lock waits before increasing limits. No full-scale performance claim is made on sparse local data.
- [ ] Retention policy and scheduled maintenance defined for expired immutable snapshot rows and expired assertion replay keys. **Current release retains them**; no background deletion job is installed. Never delete unexpired replay keys or event/order idempotency records. If retention is needed before rollout, implement an owner-approved bounded maintenance command/job. Snapshots' payload storage and per-page audits grow with use.
- [ ] Audit/ledger/idempotency archival, access controls, backups and monitoring retention approved. No business history purge or mutable ledger editing.
- [ ] Rate limits reviewed for shared NAT/proxy clients. Current integration limit is 120 requests/minute per remote IP and in-process, suitable for single local instance; multi-instance distributed limit planning belongs to hosting/hardening based on actual deployment.
- [ ] Numeric contracts reviewed: whole units only, maximum JS-safe per input; very large aggregate totals need consumer BigInt/decimal handling. Snapshot version is a decimal string and must not be compared lexically.
- [ ] Expiry uses UTC calendar dates; expires-today stock remains sellable until UTC midnight. Confirm operator timezone training and refresh cadence.
- [ ] No low-stock thresholds have been approved/configured. Dashboard correctly reports none; define an owner-approved threshold policy before adding low-stock classification.
- [ ] Monitoring consumes liveness/readiness/correlation IDs; credentials and request bodies are excluded. Alert on SQL timeouts, 409 spikes, persistent integration failures, snapshot row-capacity and audit growth.
- [ ] Recovery after database restore includes consumer source-generation/version reconciliation; old DB snapshot versions must not silently overwrite consumer state.
- [ ] Completed stock movement mistakes have an approved physical corrective procedure; no invented reversal workflow or direct SQL balance repair.

## Review evidence

See [delivery record](phases-5-8-delivery.md). Build/read-query smoke checks are distinct from owner manual acceptance. No automated tests, CI test pipeline, hosting deployment or legacy modification is included. The existing minimum-eight-character-only password policy is preserved by owner request; any stricter policy requires a separate approved change.
