# E-commerce developer integration contract

WROS owns physical stock. Legacy e-commerce owns variants, customer orders and temporary checkout reservations. This implementation changes no legacy code. The legacy developer must implement the client described here before enabling production use. No stock is deducted at checkout/acceptance, only after physical pick/pack at Ready to Deliver. Units are positive integers, maximum 9,007,199,254,740,991 per quantity; no fractional UOM.

## Machine authentication

Staff HttpOnly cookies and CSRF remain unchanged. Machine endpoints exclusively use the `IntegrationAssertion` scheme. Staff cookies cannot grant machine access and assertions cannot grant staff access.

The chosen contract is a **direct signed client assertion per request**, not an OAuth authorization server/token endpoint. It uses JWT validation principles from [RFC 7523](https://www.rfc-editor.org/rfc/rfc7523) and Microsoft's [JsonWebTokenHandler validation](https://learn.microsoft.com/en-us/dotnet/api/microsoft.identitymodel.jsonwebtokens.jsonwebtokenhandler.validatetokenasync). Send `Authorization: Bearer ASSERTION` over HTTPS. A fresh assertion is required even on GET/retry. Do not place credentials in URLs/browser storage.

Required protected header: `alg=RS256`, `typ=wros-client+jwt`, registered `kid`. Payload: `iss` and `sub` equal registered client ID; exact configured HTTPS `aud`; numeric `iat`, `exp`; `jti` fresh UUID. Expiry must be after issued-at and at most 120 seconds later; maximum issued-at age 120 seconds, maximum future issued-at 5 seconds, zero expiry clock skew. Use 90 seconds and synchronize clocks. RSA minimum 2048 bits. Caller-controlled role/scope/user-ID claims are ignored. Scopes come exclusively from server configuration. Each accepted assertion's client/jti hash is persisted under a unique database key before the endpoint runs; replay is rejected. An HTTP retry needs a **new assertion but the same business event key/body**.

### Local client setup

1. Start the apps using README. As SuperAdmin or a user with integration.manage, open E-commerce integration. Register client `legacy-shop`, name `Legacy e-commerce`, active. This creates only an identity registry, not credentials.
2. Outside the repository, create a private RSA key (example macOS/zsh; keep it with the caller):

```sh
mkdir -p "$HOME/.wros-integration"
chmod 700 "$HOME/.wros-integration"
openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out "$HOME/.wros-integration/legacy-shop.key"
chmod 600 "$HOME/.wros-integration/legacy-shop.key"
openssl pkey -in "$HOME/.wros-integration/legacy-shop.key" -pubout -out "$HOME/.wros-integration/legacy-shop-public.pem"
```

3. Configure the **public** key and allowed client in WROS User Secrets. Never supply the private key to WROS:

```sh
dotnet user-secrets set 'Integration:AllowedClients:0' 'legacy-shop' --project src/Wros.Api
dotnet user-secrets set 'Integration:Clients:0:Id' 'legacy-shop' --project src/Wros.Api
dotnet user-secrets set 'Integration:Clients:0:Scopes:0' 'inventory.snapshot' --project src/Wros.Api
dotnet user-secrets set 'Integration:Clients:0:Scopes:1' 'orders.deduct' --project src/Wros.Api
dotnet user-secrets set 'Integration:Clients:0:Keys:0:KeyId' 'key-2026-01' --project src/Wros.Api
dotnet user-secrets set 'Integration:Clients:0:Keys:0:PublicKeyPem' "$(cat "$HOME/.wros-integration/legacy-shop-public.pem")" --project src/Wros.Api
```

Restart the API; startup validates the allowlist, scopes, audience, limits and public RSA keys. Defaults leave integration disabled (`AllowedClients=[]`, `Clients=[]`). Deployment uses secure configuration, not a committed private key. On PowerShell use `Get-Content -Raw PATH` for the public PEM argument and adapt shell variable syntax; JWT utility works on both systems.

4. For local curl, trust the ASP.NET certificate and use curl's CA trust configuration; the examples use `-k` **only for local self-signed development**. Never use `-k` in production.

```sh
WROS_API=https://localhost:7243
WROS_AUDIENCE=https://localhost:7243/api/v1/integration
WROS_CLIENT=legacy-shop
WROS_KID=key-2026-01
WROS_PRIVATE_KEY="$HOME/.wros-integration/legacy-shop.key"
wros_assertion() { node scripts/integration-assertion.mjs "$WROS_CLIENT" "$WROS_AUDIENCE" "$WROS_KID" "$WROS_PRIVATE_KEY"; }
```

The utility prints a credential to stdout for piping/capture; do not enable shell tracing or save credentials in tickets/logs.

### Least privilege, disablement and rotation

Supported scopes: `inventory.snapshot` and `orders.deduct`. Provision separate client identities if only read access is needed. Both an active database registry entry **and** an allowed configured key are required. Disable a registry entry for immediate rejection of subsequent requests. To rotate: add the new public key with a new kid (maximum 3 keys), restart WROS, switch caller signing, wait beyond 120 seconds plus in-flight requests, remove the old public key and restart. Never repurpose a kid. Revocation does not roll back committed stock movements. Assertions are consumed even if downstream validation rejects the request. Rate limit: 120 integration requests per minute per remote IP; 429 means back off with jitter and a fresh assertion. No untrusted forwarded headers are accepted.

## Stable product mapping

In the integration screen create `(clientId, externalProductId) -> WROS productId`. WROS does not match names, SKU text or the old optional catalog `ExternalProductId` automatically. That existing catalog field is preserved but is not an authentication/mapping authority. External IDs are stable opaque strings; use canonical spelling/case and avoid case-only differences (SQL database collation governs unique keys).

Each external identifier has one mapping per client, including disabled mappings; edit/reactivate rather than creating duplicates. Several external variant identifiers can reference one WROS product without defining warehouse-side color/size/year variants. These references share stock: never sum the same physical lot once per variant. A missing, disabled or mismatched mapping blocks deduction with 422. Mapping changes are audited and row-version checked.

Management endpoints require staff cookie+CSRF+integration.manage:

- `GET /api/v1/integration-management?page=1&pageSize=20` — registry, configured scopes/key IDs, recent snapshots, paged deduction history.
- `POST /api/v1/integration-management/clients` — `{id,name,isActive,rowVersion}`; null version for a new registry entry.
- `GET /api/v1/integration-management/mappings?clientId=legacy-shop&page=1&pageSize=20`.
- `POST /api/v1/integration-management/mappings` and `PUT .../mappings/{id}` — `{clientId,externalProductId,productId,isActive,rowVersion}`.

## Immutable full inventory snapshots

Create:

```sh
curl -k -sS -X POST "$WROS_API/api/v1/integration/snapshots" -H "Authorization: Bearer $(wros_assertion)" -H 'Content-Type: application/json'
```

Response header example (values illustrative):

```json
{"id":"SNAPSHOT_UUID","version":"12","createdAt":"2026-10-18T10:00:00Z","expiresAt":"2026-10-18T10:30:00Z","totalRows":2,"contentHash":"SHA256_HEX","hashAlgorithm":"SHA256 of UTF-8 payload strings plus LF in ordinal order","pageSizeLimit":100}
```

A serializable transaction reads balances, current product/location labels, active mappings and sellability, then persists immutable rows. Snapshot creation is serialized with a transaction-owned SQL application lock so version order corresponds to capture/commit order. Version is a **capture generation**, not an increment-per-stock-mutation value; gaps are allowed. It is an invariant decimal string to avoid JavaScript integer rounding. UTC expiry/sellability is frozen at capture; create a new snapshot for current expiry. Staff can continue operating; long captures may briefly block writes or return a safe conflict/timeout, and the caller must retry creation with a fresh assertion. No partial snapshot is published by WROS.

Fetch pages with a new assertion each time:

```sh
WROS_SNAPSHOT=REPLACE_WITH_RETURNED_UUID
curl -k -sS "$WROS_API/api/v1/integration/snapshots/$WROS_SNAPSHOT?page=1&pageSize=100" -H "Authorization: Bearer $(wros_assertion)"
```

Each response: `{snapshot: HEADER,page,pageSize,items:[{ordinal,payload,data}],complete}`. `payload` is the exact serialized row string for integrity checking; `data` is its parsed object:

```json
{"stock":{"id":"BALANCE_UUID","productId":"PRODUCT_UUID","sku":"SKU-1","product":"Product","barcode":null,"brand":"Brand","stockLotId":"LOT_UUID","supplierBatch":"BATCH-1","expiry":"2026-12-31","receivedAt":"2026-10-17T10:00:00Z","deliveryNoteId":"DN_UUID","deliveryNoteNumber":"DN-2026-000001","warehouseId":"WAREHOUSE_UUID","warehouse":"Main","areaId":"AREA_UUID","rackId":"RACK_UUID","locationId":null,"path":"WH / AREA / RACK (rack)","quantity":10,"sellableQuantity":10},"externalProductIds":["external-variant-1"],"fifoRank":1,"fefoRank":1}
```

Rows include **all positive physical balances**, including expired/inactive-product stock with sellable zero, and unmapped products with empty external IDs. Zero balances and products without physical stock are absent; a completed full replacement must remove previous stock absent from the new snapshot. No checkout reservation data is included. Do not multiply stock by the number of external mappings. FIFO rank is by received date/lot UUID; FEFO dated first, earliest nonexpired expiry then received date/UUID, undated last. Expired/inactive products have null ranks. Locations of the same lot share its rank. Recommendations do not reserve stock; actual selected stock is revalidated at deduction.

### Consumer completeness/publication protocol

1. Create a snapshot; save ID, decimal version, expiry, totalRows and hash in a staging record. Fix pageSize 1–100 for the entire attempt.
2. Fetch every page of that **same ID**, in ordinal order. Retry missing pages with fresh assertions; never substitute a new snapshot's page halfway through.
3. Require unchanged header, exactly totalRows unique contiguous ordinals `1..totalRows`, no duplicate balance IDs, and SHA256 over the exact decoded `payload` strings plus LF in ordinal order. Do not reserialize `data` to compute the hash. The empty snapshot hash is SHA256(empty bytes).
4. `complete=true` means the returned page reaches the end, **not proof that prior pages arrived**. Independently validate count, ordinals and hash.
5. If 410 expiry, validation failure or unrecoverable network failure occurs: discard staging, keep the last published inventory, create a new snapshot and restart. Never publish a partial retrieval.
6. Atomically compare the numeric version against the last published version **for this WROS source/client**, then replace the published physical inventory in one consumer transaction. Reject less-than/equal versions. Use BigInt/decimal, not lexical comparison. Keep consumer reservations separate and reapply its own availability rules.
7. Serialized snapshot creation makes a higher version newer than a lower version. After restoring an older database backup, stop sync and reconcile/reset the consumer source-generation watermark explicitly; do not silently accept version regression.

Snapshot lifetime defaults 30 minutes (`Integration:SnapshotMinutes`, 1–1440); row capacity defaults 100,000 (`MaxSnapshotRows`, 100–1,000,000). Above capacity returns 413, never a truncated snapshot. An ID belonging to another client returns 404. An expired ID returns 410 even if rows are retained. Snapshot/replay retention is operator-managed; see production readiness before sustained use.

## Ready to Deliver deduction

```sh
curl -k -sS -X POST "$WROS_API/api/v1/integration/ready-to-deliver" \
  -H "Authorization: Bearer $(wros_assertion)" -H 'Content-Type: application/json' \
  --data-binary @ready-to-deliver.json
```

Local request file example (replace all UUIDs with real snapshot values; never include tokens):

```json
{
  "eventId":"ready-order-1001-v1",
  "externalOrderId":"order-1001",
  "lines":[{
    "externalLineId":"line-1","externalProductId":"external-variant-1","productId":"PRODUCT_UUID","quantity":8,
    "selections":[
      {"balanceId":"BALANCE_UUID_A","stockLotId":"LOT_UUID_A","warehouseId":"WAREHOUSE_UUID_A","rackId":"RACK_UUID_A","locationId":null,"quantity":3},
      {"balanceId":"BALANCE_UUID_B","stockLotId":"LOT_UUID_B","warehouseId":"WAREHOUSE_UUID_B","rackId":"RACK_UUID_B","locationId":"POSITION_UUID_B","quantity":5}
    ]
  }]
}
```

The caller chooses actual picked lots/locations. Each line's allocation sum must equal its positive whole line quantity. Every supplied identifier must match the selected balance/product/mapping; locations and their ancestors/product must be active, lot not expired, stock sufficient across all repeated selections. Maximum 500 order lines / 5,000 selections / 2 MB. The service validates every selection and deducts all lines with immutable ledger/audit in one serializable database transaction. No partial deduction. Lot identity, original DN and receipt allocations remain unchanged.

Success (including identical retry): `{id,clientId,eventId,externalOrderId,completedAt,correlationId,status:"Deducted"}`. The same client/event and identical typed request returns the original stored result, even after the stock has moved/expired. Keep the original field values and array order for retries. Changed event-body content returns 409. A second event key for an already deducted external order also returns 409 to prevent accidental double stock-out; order IDs must be unique per client. One event handles the complete Ready-to-Deliver order, not partial shipments under reused order IDs. An unsuccessful transaction does not reserve its event key; correct invalid selections and retry only after confirming no success occurred.

### Error and retry contract

Errors use ProblemDetails with safe title and correlationId; do not parse exception text.

| Status | Meaning | Caller action |
|---|---|---|
| 400 | Invalid shape, quantity/sum, inactive source | Correct input; don't finalize RTD |
| 401 | Missing/invalid/expired/replayed assertion, unknown/disabled client/key | Correct credentials; generate fresh assertion |
| 403 | Missing server-assigned scope | Operator must grant minimal scope |
| 404 | Unknown/cross-client snapshot | Check snapshot/client |
| 409 | Insufficient stock, event conflict, already deducted order, concurrent write/deadlock | Keep order pending; refresh/reconcile. Retry identical event after timeout/conflict only when appropriate |
| 410 | Snapshot expired | Discard staged pages and restart |
| 413 | Snapshot row capacity/request body exceeded | Review configured capacity or reduce request within limits; never publish truncation |
| 422 | Missing/mismatched mapping or selection, expired lot | Correct mapping/physical picking; don't finalize RTD |
| 429 | Rate limited | Backoff, fresh assertion, same business event/body |
| 500/503 or network timeout | Outcome might be unknown | Retry original event/body with fresh assertion until confirmed; escalate correlation ID |

### Cross-system consistency and reconciliation

E-commerce must retain a durable pending event and exact selections, call WROS, and finalize Ready to Deliver **only on confirmed Deducted**. If a timeout follows a committed deduction, retrying the same event returns the original outcome without another deduction. Do not generate a new event key to bypass an unknown outcome. If WROS rejects stock, keep the e-commerce order pending; correct physical selections or mapping and coordinate staff before retry. No warehouse-side reservation or automatic rollback/cancellation is implemented.

If legacy cannot gate RTD on success, production enablement is blocked until an owner-approved pending/failed-event reconciliation process exists. Staff review Integration recent deductions, ledger RTD reference and correlation ID against external order events. Fix missing events via a retry of the original valid event; investigate conflicting/duplicate events manually. Never change balances in SQL, erase ledger rows or blindly resend with a new order identity. Returned/canceled goods need the applicable physical warehouse process, not deletion of a completed deduction.
