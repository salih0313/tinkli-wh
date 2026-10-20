# Configuration reference

Sources: appsettings.json non-secret keys/defaults; appsettings.Development.json non-secret local overrides; .NET User Secrets for Development; environment variables/secret vault for deployment. `appsettings.example.json` is illustrative, not an automatically loaded secret file. Environment paths use double underscores (`ConnectionStrings__DefaultConnection`). Startup uses Options validation and safe errors.

| Key | Meaning / limits |
|---|---|
| ConnectionStrings:DefaultConnection | Real SQL connection from secrets; database required; Persist Security Info disabled; LocalDB Windows only; trusted encryption outside Development |
| Authentication:SessionMinutes | Fixed secure staff cookie session, 1–60 minutes, existing default 15 |
| Jwt:Issuer / Audience / AccessTokenMinutes | Retained foundation configuration; staff auth uses the approved cookie architecture, not a second JWT login mechanism |
| Cors:AllowedOrigins | Explicit origins only, HTTPS outside Development; staff mutations are same-origin proxy + CSRF, not wildcard cross-origin cookie calls |
| Inventory:ExpiryAlertDays | 0–3650, default 30, UTC date window including today |
| Integration:AllowedClients | Explicit permitted machine client IDs; empty disables all callers |
| Integration:Audience | Exact HTTPS URI, local default https://localhost:7243/api/v1/integration |
| Integration:Clients | Client id, minimal scopes and 1–3 registered public RSA keys; must also have active DB registry |
| Integration:Clients:N:Scopes | inventory.snapshot and/or orders.deduct, never caller-supplied claims |
| Integration:Clients:N:Keys:N:KeyId / PublicKeyPem | Public RSA key only, >=2048 bits; private keys rejected and belong to caller |
| Integration:SnapshotMinutes | 1–1440, default 30 |
| Integration:MaxSnapshotRows | 100–1,000,000, default 100,000; exceeding it rejects capture rather than truncating |
| Seed:Enabled / SampleDataEnabled | Retained compatibility flags must stay false; no sample business transaction seed is provided |
| ProductionAdminSeed:Enabled / UserName / Password | Opt-in first production SuperAdmin bootstrap; disabled by default. Supply credentials through secret configuration, then remove them after bootstrap. See [setup](production-admin-seed.md). |
| Logging / Serilog | Structured logs, correlation IDs; EF sensitive logging disabled; never enable token/body/SQL credential logging |
| AllowedHosts | Explicit deployment hostnames; local localhost/127.0.0.1 defaults |

See [local setup](local-development.md) for existing private-file Development user credentials. That previously approved exception is retained, but real account values must stay in the ignored local file, never in the tracked example/appsettings. Debug builds compile that owner-approved local file; Release builds explicitly exclude it and compile the empty example. Deployment uses secure secret provisioning; no demo users are created outside Development. The tracked example was cleared during Phase 8; rotate any accounts that previously used its known example values.

Keys/scopes are startup-validated and read through IOptions; restart after rotation/configuration changes. Database client active state is checked on every machine authentication. Machine private keys, signatures, real passwords and production connections must not enter Git, logs or issue bodies.

SQL command timeout remains 10 seconds. Serializable stock operations may return safe conflicts/deadlocks instead of overselling. Snapshot capture application-lock timeout is 5 seconds. Page size maximum 100; most UI pages use 20. Max input document sizes and line limits are enforced server-side. No Redis/cache/broker is required.
