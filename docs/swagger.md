# Development Swagger UI

Swagger UI uses the existing OpenAPI document and is available only in Development.

From the repository root:

```sh
dotnet restore Wros.slnx --locked-mode
dotnet run --project src/Wros.Api --launch-profile https
```

Open **https://localhost:7243/swagger** (redirects to `/swagger/index.html`). Trust the ASP.NET development HTTPS certificate if needed using `dotnet dev-certs https --trust`.

Use the existing SQL configuration and development accounts. No new configuration, migration or seed changes are required. Normal API startup still applies pending database migrations; use the existing development database.

## Manual verification

1. Open `/swagger`. Expected: WROS API v1 loads, showing grouped endpoints, schemas and a filter. `/openapi/v1.json` continues returning JSON.
2. Expand `POST /api/v1/auth/login`, select **Try it out**, enter your existing username/password in the request body, then **Execute**. Expected: 204 and a secure HttpOnly session cookie. Swagger automatically fetches a CSRF token before writes; no manual cookie/token copying is required.
3. Execute `GET /api/v1/auth/me`. Expected: 200 with your current user and permissions. If a temporary password must be changed, execute `POST /api/v1/auth/change-password` before other operations.
4. Execute a list endpoint permitted for your account (for example products or inventory). Expected: 200 with current database data. Use query fields to filter/page results.
5. With an authorized account, create or update a disposable development record using a documented request body, then read it back. Expected: normal validation, permissions and concurrency rules apply, with no CSRF error. Requests change the connected database.
6. Execute `POST /api/v1/auth/logout`, then `GET /api/v1/auth/me`. Expected: 204 then 401. Log in with WarehouseStaff and call a SuperAdmin endpoint: expected 403.
7. In an already configured non-Development instance, request `/swagger/index.html` and `/openapi/v1.json`. Expected: neither serves documentation (the fallback authorization policy may return 401 instead of 404 for anonymous requests).

## Limitations

Swagger is an API explorer, not a database browser or inventory report. Authorization and rate limits remain enforced. Staff authentication uses cookies; machine integration still requires its separately signed client assertion and scopes, as described in the integration guide. This change does not add a Swagger authorization flow for machine callers.

Implementation uses the standalone UI package with the existing built-in OpenAPI generator; see [Swashbuckle UI configuration](https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/configure-and-customize-swaggerui.md).

Please verify these checks and approve this Swagger addition before further work.
