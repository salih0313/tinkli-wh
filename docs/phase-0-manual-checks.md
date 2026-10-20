# Phase 0 manual acceptance

Historical checklist: Phase 0 was approved by the owner. Phase 1 changes authentication, frontend routing and seed behavior; use [phase-1-manual-checks.md](phase-1-manual-checks.md) for current acceptance.

Run the setup in [README](../README.md) first. Use a dedicated Wros development database, never the legacy database. On Windows substitute `curl.exe` for `curl` where needed. No automated tests accompany this checklist.

## Owner checks

1. **Restore and build.** From the root run:
   ```sh
   dotnet restore Wros.slnx --locked-mode
   dotnet tool restore
   dotnet build Wros.slnx --no-restore
   npm ci --prefix src/wros-web
   npm run build --prefix src/wros-web
   ```
   Expected: .NET build has zero errors and warnings; TypeScript and Vite produce `src/wros-web/dist`. No test suite runs.

2. **Apply the baseline migration.** Configure `ConnectionStrings:DefaultConnection` through User Secrets per README, then run:
   ```sh
   dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
   dotnet ef migrations list --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
   ```
   Expected: `InitialFoundation` is applied. The Wros database contains only the EF migration history table, with one baseline row. There are no Identity/business tables. Run `database update` again: no changes, no destructive reset. No seeding command exists in Phase 0.

3. **Launch both applications.** Terminal 1:
   ```sh
   dotnet run --project src/Wros.Api --launch-profile https
   ```
   Terminal 2:
   ```sh
   npm run dev --prefix src/wros-web
   ```
   Visit `http://localhost:5173`.
   Expected: the responsive WROS foundation screen loads, checks status, and shows Healthy for the API and database after migration. The page offers a refresh button, correlation references, and development OpenAPI link. No inventory/business screens or login are implemented. If ports 7243/5243 or 5173 are occupied, use other free ports consistently in launch settings, frontend API URL and CORS configuration.

4. **Check system, liveness and readiness independently.**
   ```sh
   curl -i https://localhost:7243/api/v1/system
   curl -i https://localhost:7243/api/v1/health/live
   curl -i https://localhost:7243/api/v1/health/ready
   ```
   Expected: all return 200. System identifies WROS/v1/Phase 0; health status is Healthy. Ready includes the database check; live has no dependencies. Responses include `X-Correlation-ID`, `X-Content-Type-Options: nosniff` and `Cache-Control: no-store`; no server banner or credentials. Trust the development certificate; do not accept a permanent `-k` workaround.

5. **Open development OpenAPI.** Visit `https://localhost:7243/openapi/v1.json` or the page link.
   Expected: 200 JSON OpenAPI document documenting `/api/v1/system`. Health checks are middleware endpoints documented in README. The diagnostics probe is intentionally hidden from OpenAPI. Swagger UI is now available in Development at `/swagger`; see [Swagger manual checks](swagger.md).

6. **Verify safe errors and correlation.**
   ```sh
   curl -i -H "X-Correlation-ID: 11111111-2222-4333-8444-555555555555" https://localhost:7243/api/v1/development/diagnostics/error
   curl -i https://localhost:7243/api/v1/missing
   curl -i -H "X-Correlation-ID: invalid-value" https://localhost:7243/api/v1/health/live
   ```
   Expected: the probe returns 500 `application/problem+json`, a generic title/detail, traceId, and the supplied correlationId. The same UUID appears in the response header and JSON logs. No stack trace, SQL detail, password or token appears in the response. Missing route returns 404 ProblemDetails. Invalid correlation input is replaced by a generated UUID. Logs include request method/path/status/timing and safe exception type, without raw error probe message, request bodies or query strings.

7. **Verify HTTP redirects and CORS.**
   ```sh
   curl -I http://localhost:5243/api/v1/system
   curl -i -X OPTIONS -H "Origin: http://localhost:5173" -H "Access-Control-Request-Method: GET" -H "Access-Control-Request-Headers: X-Correlation-ID" https://localhost:7243/api/v1/health/live
   curl -i -X OPTIONS -H "Origin: https://untrusted.example" -H "Access-Control-Request-Method: GET" https://localhost:7243/api/v1/health/live
   ```
   Expected: HTTP redirects 307 to HTTPS on 7243. Allowed preflight returns 204 and `Access-Control-Allow-Origin: http://localhost:5173`. Disallowed preflight has no Access-Control-Allow-Origin, preventing browser cross-origin access. CORS is not authentication; only public foundation endpoints exist.

8. **Verify database failure without affecting a real database.** Stop the API, then launch with a deliberately unreachable credential-free connection:
   ```sh
   dotnet run --project src/Wros.Api --launch-profile https -- --ConnectionStrings:DefaultConnection="Server=127.0.0.1,1;Database=Wros;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Connect Timeout=2"
   ```
   Refresh the page and repeat live/ready requests.
   Expected: live stays 200/Healthy; ready returns 503/Unhealthy with database status only, without SQL errors or connection details. Frontend displays database Unhealthy and can refresh. Stop this process and return to the normal launch command; saved User Secrets are unchanged.

9. **Verify startup validation.** Stop the API, then run:
   ```sh
   dotnet run --project src/Wros.Api --launch-profile https -- --ConnectionStrings:DefaultConnection=
   ```
   Expected: startup exits nonzero and clearly identifies the missing/invalid `ConnectionStrings:DefaultConnection`. No database changes occur. Also try `--Inventory:ExpiryAlertDays=-1` with the normal valid connection: it fails validation. `--Seed:Enabled=true` fails with the explicit Phase 0 seeding message. Return to the normal launch command afterwards.

10. **Verify frontend error recovery and tablet layout.** While the page is open, stop the API and click Refresh status. Then restart it and refresh again. Resize the browser to 768px and 375px widths, use Tab to focus the refresh button and OpenAPI link, and activate Refresh with the keyboard.
    Expected: requests settle with visible connection errors (15-second timeout maximum), the button re-enables, and the page recovers after API restart. Cards stack on narrow displays; content is readable without horizontal page scrolling; focus is visible. Loading status is announced through a polite live region. Complete this check in an actual browser.

11. **Verify environment isolation (local process only).** Stop the development API. Supply a temporary `ConnectionStrings__DefaultConnection` environment variable through the secure prompt approach in README, using `Encrypt=True;TrustServerCertificate=False`, and an HTTPS CORS origin. User Secrets do not load in Production. Run:
    ```sh
    dotnet run --project src/Wros.Api --no-launch-profile -- --environment Production --urls https://localhost:7343 --Cors:AllowedOrigins:0=https://localhost:5173
    ```
    Expected: `/api/v1/system` on 7343 responds, but `/openapi/v1.json` and `/api/v1/development/diagnostics/error` both return 404. Invalid/empty production SQL or missing CORS origins fail startup; TrustServerCertificate=True is rejected. This checks environment guards only; no production deployment or readiness claim is made. Stop this process and remove the temporary environment variable before restarting Development.

## Implementation verification record — 2026-10-16

- Read all root `AGENTS.md`; initial repository contained that file only and was not a Git repository.
- SDK 10.0.401, runtime 10.0.12, Node 22.18.0 and npm 10.9.3 available.
- Backend restore/build succeeded with zero warnings/errors, including generated migration.
- Frontend production build succeeded; npm install audit reported zero vulnerabilities.
- Generated migration SQL inspected: creates only EF history table and inserts the baseline row. No database update was executed.
- Launched Development API and Vite; Vite served the frontend entry successfully.
- HTTPS worked with certificate verification in curl. System/live returned 200; unreachable database readiness returned 503 without connection detail.
- Verified error probe 500 ProblemDetails, unknown route 404, supplied UUID propagation and invalid-ID replacement.
- Verified HTTP 307 redirect, allowed/disallowed CORS preflights, and correlation-header exposure to the allowed origin.
- Missing-connection startup failed clearly and nonzero.
- A temporary local Production-mode process returned 404 for both OpenAPI and the error probe, while system info remained 200.
- Public dependencies were restored with sandbox network approval. Development runtime/EF processes required running outside the sandbox. No credentials were read from the existing SQL container, and no existing database was modified.
- Local runtime observation: some concurrent HTTP/2 curl requests produced TLS shutdown `Bad address` errors in Kestrel logs on this macOS/.NET 10.0.12 host, after successful responses. Use a current .NET 10 servicing SDK/runtime; recheck if encountered during owner browser verification. These infrastructure errors are not suppressed.
- **Pending owner verification:** applying migration to the owner's Wros database, successful SQL readiness, idempotent migration reapplication, actual browser rendering, keyboard/tablet behavior and frontend recovery. These require owner configuration/browser acceptance. No automated tests were created or run.

## Owner acceptance gate

Record pass/fail for the numbered checks, report any error with its correlation ID (never send secrets), and explicitly confirm Phase 0 acceptance before Phase 1 begins.
