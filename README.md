# WROS — Phase 1: Identity and access

WROS is the physical inventory source of truth. Phase 0 is owner-approved. Phase 1 adds authentication, account administration, permissions and security auditing. No master-data or warehouse business operations, integration endpoints, automated tests, CI or deployment are implemented. The legacy e-commerce application and database remain untouched.

## Architecture

One .NET 10 controller API, SQL Server/EF Core 10 and a React/TypeScript/Vite/Tailwind SPA. Identity uses the **secure HttpOnly cookie alternative permitted by AGENTS.md**, not JWTs or browser-stored tokens. Cookies are Data Protection-protected, Secure, HttpOnly, SameSite=Strict and host-only. Sessions have a fixed 15-minute lifetime by default, without sliding renewal. Every authenticated application request validates the current Identity security stamp, active status and lockout against the database. Public health probes ignore session cookies, keeping liveness independent of SQL.

The SPA makes same-origin `/api` requests. Local Vite forwards them to `https://localhost:7243`; it does not expose cookies to JavaScript. State changes, including login/logout, require a server-issued antiforgery cookie plus `X-CSRF-TOKEN`; the typed client fetches a token before each mutation. A production frontend/API must share one HTTPS origin; hosting is not implemented here. Local development uses `http://localhost:5173`, which supported browsers treat specially for Secure localhost cookies. If a browser rejects those cookies, use a trusted same-origin HTTPS development setup rather than disabling cookie security. Do not use a LAN address or `127.0.0.1` for the frontend in this setup.

Permissions are database-driven and queried on each protected request. SuperAdmin always has full access; user/role administration additionally requires the SuperAdmin role even if a staff role is granted users.manage or roles.manage. All warehouse access remains global in v1. Owner-approved initial staff mapping: **inventory.view only for every staff role**. SuperAdmin may edit these grants from Roles. The role data migration preserves edits.

## Project structure and changed files

```text
Wros.slnx / global.json / .config/dotnet-tools.json    Phase 0 foundation retained
src/Wros.Api/
  Program.cs                                         Auth middleware, guards and EF seeding callbacks
  Wros.Api.csproj / packages.lock.json                Identity EF package added
  appsettings.json / appsettings.example.json         Session configuration; no seed account credentials
  Controllers/
    AuthController.cs                                CSRF, login, current session, password, logout
    UsersController.cs                               SuperAdmin user administration
    RolesController.cs                               SuperAdmin permission administration
    AuditController.cs                               Paginated security audit
    SystemController.cs                              Phase label updated; public foundation endpoint
    DevelopmentDiagnosticsController.cs              Retains explicit Development-only access
  Modules/Identity/
    Entities.cs / Contracts.cs                        Identity entities and validated DTOs
    PermissionCatalog.cs / PermissionAuthorization.cs Central grants and policies
    CookieSessionEvents.cs / IdentityRegistration.cs  Cookie, CSRF, lockout and rate-limit setup
    SessionService.cs                                Authentication application service
    DevelopmentUserSeed.cs                           Development-only seed on EF database update
    DevelopmentSeedCredentials.example.cs             Empty tracked C# credential template
    DevelopmentSeedCredentials.local.cs               Private editable C# passwords (Git ignored)
    UserAdministration.cs / RoleAdministration.cs     Transactional administration services
    AuthorizationAuditHandler.cs                     Safe authorization-denial audit
    README.md
  Modules/Audit/
    AuditLog.cs / AuditService.cs / README.md          Central safe audit recording
  Shared/
    Configuration/FoundationOptions.cs               Session options and disabled legacy seed flags
    Errors/ApiProblemException.cs                    Expected safe API errors
    Errors/ApiExceptionHandler.cs                     Expected errors and concurrency conflicts
    Persistence/WrosDbContext.cs                      Identity schema and indexes
    Persistence/Migrations/
      20261016110833_InitialFoundation*                Preserved unchanged
      20261016122831_Phase1Identity*                   New additive migration + designer
      20261016155337_SeedIdentityData*                 Role/permission data migration
      20261016164608_SeedMissingDevelopmentUsers*      Compatibility migration ID; user seed is repeatable
      WrosDbContextModelSnapshot.cs                    Updated model snapshot
src/wros-web/
  package.json / package-lock.json                    React Router added
  vite.config.ts / .env.example                       Same-origin local API proxy
  src/App.tsx / src/styles.css                        Routes, navigation and reusable styles
  src/api/client.ts / src/api/types.ts                Typed credential-safe API client
  src/auth/SessionContext.tsx                         Current session and expiry handling
  src/components/Form.tsx                            Accessible forms and status messages
  src/pages/
    Login.tsx / Home.tsx / Password.tsx
    Users.tsx / Roles.tsx / Audit.tsx / Foundation.tsx
README.md
docs/phase-0-manual-checks.md                         Historical Phase 0 checklist retained
docs/phase-1-manual-checks.md                         Current acceptance checklist
```

The remaining module folders are placeholders. No generic repositories, separate auth service, event bus, cache or microservices were introduced.

## Required software

- Current .NET **10 LTS SDK**; `global.json` permits 10.0 feature bands.
- Node.js **22.12+**, npm and VS Code with the C# extension if debugging.
- A reachable SQL Server **2022+** development instance and a dedicated `Wros`/`TinkliWH` database. Never target the legacy database. SQL Server does not run natively on macOS; use an appropriate local container or remote development server.
- Trusted ASP.NET HTTPS development certificate.

NuGet dependencies: ASP.NET Core Identity EF/OpenAPI and EF Core SQL Server/Design 10.0.12, local dotnet-ef 10.0.12, Serilog.AspNetCore 10.0.0 and compact JSON formatter 3.0.0. Cryptography Pkcs is pinned to patched 10.0.13. Frontend: React 19, React Router 7, TypeScript 5.9, Vite 7, Tailwind 4. Exact resolutions are locked in the package lockfiles.

## Restore and configure

From the repository root (commands work in zsh and PowerShell unless specified):

```sh
dotnet dev-certs https --trust
dotnet restore Wros.slnx --locked-mode
dotnet tool restore
dotnet build Wros.slnx --no-restore
npm ci --prefix src/wros-web
npm run build --prefix src/wros-web
```

The project already has a User Secrets ID. Store the development connection securely; the following contains placeholders only:

```sh
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost,1433;Database=Wros;User Id=YOUR_DEV_SQL_USER;Password=YOUR_DEV_SQL_PASSWORD;Encrypt=True;TrustServerCertificate=True;Persist Security Info=False;Connect Timeout=5" --project src/Wros.Api
```

Real passwords never belong in appsettings, `.env`, VS Code files or source control. The example command can enter shell history; the prompt approaches below avoid literal secrets in history. User Secrets are local development storage, not an encrypted production vault. Do not send `user-secrets list` output to chat or logs.

macOS zsh prompt for the full connection:

```sh
read -rs 'wros_connection?WROS development SQL connection string: '
printf '\n'
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' "$wros_connection" --project src/Wros.Api
unset wros_connection
```

For bash use `read -r -s -p 'WROS development SQL connection string: ' wros_connection` instead of the zsh read line.

PowerShell:

```powershell
$wrosSecureConnection = Read-Host 'WROS development SQL connection string' -AsSecureString
$wrosConnection = [System.Net.NetworkCredential]::new('', $wrosSecureConnection).Password
dotnet user-secrets set 'ConnectionStrings:DefaultConnection' $wrosConnection --project src/Wros.Api
Remove-Variable wrosConnection, wrosSecureConnection
```

Configuration keys remain in `appsettings.json`, with non-secret local overrides in `appsettings.Development.json` and placeholders in `appsettings.example.json`. Examples are not loaded automatically. `Authentication:SessionMinutes` controls cookie lifetime (1–60, default 15). The retained Jwt section is unused by cookie authentication; no JWT signing key is needed. `Seed:SampleDataEnabled` must remain false and `Integration:AllowedClients` empty until those phases. Set `ConnectionStrings__DefaultConnection` and other settings through secure environment variables/vault outside Development; User Secrets are not loaded in Production.

SQL encryption, trusted server certificates, HTTPS CORS origins and explicit hosts are required outside Development. `TrustServerCertificate=True` is local Development only. Keep the committed connection placeholder empty. A valid unreachable SQL connection allows startup but readiness/authentication will fail until SQL recovers. Options validate configuration at startup; migration application is explicit, never automatic.

### LocalDB error on macOS/Linux

`Server=(localdb)\\MSSQLLocalDB` selects Windows-only LocalDB, not your SQL Server container. Use a TCP endpoint such as `localhost,1433` and the configured SQL login. Do not use Trusted_Connection=True for a normal SQL-login container connection. LocalDB is rejected on unsupported platforms with a clear message.

Environment variables override User Secrets. To remove an obsolete shell connection override, use `unset ConnectionStrings__DefaultConnection` in zsh/bash, or `Remove-Item Env:ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue` in PowerShell.

## Apply migrations and seed users directly from C#

The owner requested file-based Development seeding with **no opt-in flag, no seed environment variables and no appsettings passwords**. Enter the two passwords in:

```text
src/Wros.Api/Modules/Identity/DevelopmentSeedCredentials.local.cs
```

This local C# file is ignored by Git. Its two fields are `SuperAdminPassword` and `WarehouseStaffPassword`; the account names are wros.admin and wros.staff. The tracked `DevelopmentSeedCredentials.example.cs` contains empty fields so clean clones still build. If the local file is absent, copy the example once:

macOS:

```sh
cp src/Wros.Api/Modules/Identity/DevelopmentSeedCredentials.example.cs src/Wros.Api/Modules/Identity/DevelopmentSeedCredentials.local.cs
```

PowerShell:

```powershell
Copy-Item src/Wros.Api/Modules/Identity/DevelopmentSeedCredentials.example.cs src/Wros.Api/Modules/Identity/DevelopmentSeedCredentials.local.cs
```

Edit the local file and set both passwords. Use at least 8 characters. Keep the example empty; passwords are never placed in tracked migrations, appsettings, or generated model snapshots.

Then run this one command from the repository root:

```sh
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
```

EF applies schema/role migrations and then invokes `Modules/Identity/DevelopmentUserSeed.cs` through its built-in UseSeeding/UseAsyncSeeding callbacks. The callbacks run **even when no migrations are pending**, so an already-applied migration cannot silently prevent missing user creation. The normal command builds the edited local C# file automatically; do not use --no-build after changing its passwords.

Behavior:

- Development only; no Development user callback is registered outside Development.
- Missing users are created atomically with Identity password hashes, role memberships and safe audit records. Plaintext passwords are not sent to SQL or audit logs.
- Existing usernames are skipped. Passwords, activation and role assignments are never reset by rerunning database update or editing the local seed file. Use the authenticated account-management password flow for an existing account.
- Missing/weak passwords for a missing account produce a clear error naming the local file. There is no silent skip and no default password. If both users already exist, no seed credentials are required.
- Roles/permissions are still inserted by the role data migration in every environment, preserving edited staff grants. SeedMissingDevelopmentUsers retains its existing migration ID as a compatibility marker; actual Development user creation now runs in the EF callback. Schema migrations and existing database rows are preserved.
- No automatic migration or seeding occurs when starting/debugging the API. No database reset or migration-history edit is needed.
- Seed:Enabled stays false as a legacy flag; it does not control this file-based flow. Seed:SampleDataEnabled remains false until its future phase.
- SQL migration scripts do not execute EF seeding callbacks; use the command above for local Development accounts. Production never creates these demo accounts.

Verify and repeat safely:

```sh
dotnet ef migrations list --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
```

Expect the existing four migration IDs applied and exactly the missing Development users inserted. The second update makes no password/role changes. No additional schema migration is required for the callback refactor. The previous opt-in/password environment variables are obsolete and ignored.

| Role | Initial permissions | Default Development account |
| --- | --- | --- |
| SuperAdmin | All 12 permissions | wros.admin |
| WarehouseManager | inventory.view | — |
| WarehouseStaff | inventory.view | wros.staff |
| PurchasingStaff | inventory.view | — |
| InventoryController | inventory.view | — |
| Viewer | inventory.view | — |

Permissions: users.manage, roles.manage, suppliers.manage, products.manage, purchaseorders.manage, receiving.manage, inventory.view, inventory.adjust, inventory.transfer, outbound.manage, integration.manage, audit.view.

SuperAdmin grants are fixed/full. Staff mappings are editable and additive across assigned roles. Granting users.manage/roles.manage to staff does **not** confer SuperAdmin administration privileges. Production user bootstrap remains an operational decision; the EF callbacks never create demo administrators outside Development.

## Run and debug

Terminal 1:

```sh
dotnet run --project src/Wros.Api --launch-profile https
```

Terminal 2:

```sh
npm run dev --prefix src/wros-web
```

Open **http://localhost:5173** and sign in with the configured Development account. The API listens on **https://localhost:7243** and redirects HTTP 5243 to HTTPS. Vite forwards `/api` and `/openapi` to that API. **Restart Vite after this Phase 1 configuration change.** `VITE_API_BASE_URL` is no longer used; authentication intentionally uses the same origin. If changing API ports, update launchSettings.json, `.vscode/launch.json` and the target in vite.config.ts together.

The proxy disables certificate validation only for its local upstream Development connection. It is never a production TLS setting. Browser sessions and CSRF cookies remain Secure/HttpOnly. Keep localhost consistent across tabs and tools; same-host cookies are shared across ports. Do not run separate WROS instances with the same cookie names while manually testing sessions.

VS Code: open the repository root, install the recommended C# extension, select **WROS API (Development)** in Run and Debug. F5 builds/debugs; Run → Run Without Debugging runs normally. Stop any API already using 7243/5243 before launching another. No VS Code profile or frontend config contains credentials.

## Authentication and administration behavior

- Login uses username/password; no public registration or password-reset delivery endpoint exists.
- Five failed password attempts lock the account for 15 minutes. Unknown names, bad passwords, inactive and locked users all receive the same generic 401 message.
- Login is rate limited to 15 requests/IP/minute; password/reset/create/unlock operations to 10/user/minute. Queuing is disabled; excess requests return 429. Single-process limits are sufficient for this local modular monolith; distributed hosting is deferred.
- Every request checks active/locked state and security stamp. Deactivation, role reassignment and password changes/resets invalidate previous sessions. Permission grant edits take effect on the next authorized request.
- Logout invalidates **all sessions for that user**, including replay of previously issued cookies. Closing a browser ends the nonpersistent browser session; ticket expiry still enforces the fixed server deadline. There is no refresh endpoint or silent lifetime extension.
- Staff accounts created/reset by SuperAdmin receive temporary passwords and must change them before accessing other endpoints. The original password is required for self-service change, which signs out all sessions. Development seed passwords are owner-supplied initial credentials, not temporary admin resets.
- SuperAdmin can create accounts, activate/deactivate, assign existing roles, reset passwords and clear lockout. No account deletion or arbitrary role creation is provided. Usernames are stable after creation; no email/PII is collected for these flows.
- A SuperAdmin cannot deactivate themselves or remove their own SuperAdmin role, preventing accidental loss of the current administrative access. Their own password must be changed using Change password.
- User/role edits require current concurrency stamps. Stale edits return 409; refresh before resubmitting. Audited mutations use SQL transactions; no partial role assignments or permission replacements are committed.
- Security audit is append-only through the API and permission-protected. Actor IDs come from validated claims (or successfully validated credentials for login success), never a frontend-provided actor ID. Anonymous login attempts and system seed operations have null actors. Safe old/new projections omit passwords, hashes, security stamps, tokens, emails and request bodies. SQL/EF sensitive logging remains disabled.

ASP.NET Data Protection uses its default local key storage with application name Wros. A local warning about keys being persisted unencrypted is expected on macOS without an OS encryptor. Production key persistence/protection, identity bootstrap, hosting and operational rotation require explicit deployment design; no production-readiness claim is made by this phase.

## API reference and manual acceptance

Development OpenAPI: `https://localhost:7243/openapi/v1.json`. Interactive Swagger UI is not bundled. Cookie/CSRF requirements are documented here; OpenAPI alone does not perform login/CSRF setup.

| Endpoint | Access / behavior |
| --- | --- |
| GET /api/v1/health/live, /ready | Public process/SQL readiness |
| GET /api/v1/system | Public application info |
| GET /api/v1/auth/csrf | Public antiforgery bootstrap; returns request token, sets HttpOnly cookie |
| POST /api/v1/auth/login | Anonymous + CSRF + login throttle |
| GET /api/v1/auth/me | Authenticated session, roles, current permissions, expiry |
| POST /api/v1/auth/change-password | Authenticated + CSRF + throttle |
| POST /api/v1/auth/logout | Authenticated + CSRF; revokes all user sessions |
| GET/POST /api/v1/users | SuperAdmin + users.manage; paginated list/create |
| PUT /api/v1/users/{id} | SuperAdmin; activation and role replacement with concurrencyStamp |
| POST /api/v1/users/{id}/reset-password | SuperAdmin; new temporary password + concurrencyStamp |
| POST /api/v1/users/{id}/unlock | SuperAdmin; concurrencyStamp |
| GET /api/v1/roles, /roles/permissions | SuperAdmin + roles.manage |
| PUT /api/v1/roles/{id}/permissions | SuperAdmin; permissions array + concurrencyStamp |
| GET /api/v1/audit | audit.view; paginated security events |

List defaults: page=1, pageSize=20 (maximum 100), deterministic ordering. Users supports `search` by username (up to 64 characters). `/users/{id}` is an administrative **target**, never the actor identity. Self-service auth endpoints have no user ID parameter. Authorization is enforced by the API, independent of UI navigation. Auth errors return 401/403 ProblemDetails, never login-page redirects. Blank status errors, validation failures and expected failures retain correlation IDs.

Complete [docs/phase-1-manual-checks.md](docs/phase-1-manual-checks.md), including login, lockout, account actions, permissions, CSRF, audit and session expiry. Phase 0's checklist is retained as history. No automated tests/frameworks or CI have been added.

**Stop after Phase 1. Owner manual results and explicit approval are required before Phase 2.**

References: [ASP.NET antiforgery](https://learn.microsoft.com/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0), [ASP.NET rate limiting](https://learn.microsoft.com/aspnet/core/performance/rate-limit?view=aspnetcore-10.0), [Identity security-stamp validation](https://source.dot.net/Microsoft.AspNetCore.Identity/SecurityStampValidator.cs.html).

Seeding reference: [EF Core UseSeeding and UseAsyncSeeding](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding).


## Phases 2–4: operational modules

Master data, purchase orders and delivery-note receiving are implemented on the existing Identity and UI foundation. See the [delivery guide](docs/phases-2-4-delivery.md) for APIs, migrations, decisions and changed files. Owner manual verification remains required:

- [Phase 2 — master data](docs/phase-2-manual-checks.md)
- [Phase 3 — purchasing](docs/phase-3-manual-checks.md)
- [Phase 4 — receiving and stock-in](docs/phase-4-manual-checks.md)

Run the existing database update command to apply the three additive migrations. No database reset, extra secrets or sample transaction seed is required. Phase 5 has not been started.
