# FTP deployment setup

Two independent workflows build and deploy on pushes to `main`. API changes deploy only the API; frontend changes deploy only the frontend; changes to both deploy both. Each workflow file also triggers its own deployment when edited. API build configuration changes trigger API deployment. Documentation-only changes deploy neither. Each selected app uploads changed build files and removes obsolete files previously tracked by its FTP state file.

## 1. Prepare the two hosts

- Backend: install the .NET 10 ASP.NET Core runtime (Hosting Bundle for IIS), configure the application/start command and HTTPS, SQL Server access and production environment. The workflow publishes a framework-dependent application; it does not provision hosting.
- Frontend: configure a static website for `dist` contents, HTTPS and SPA fallback to `index.html` for browser routes.
- **Set the API URL in GitHub:** repository → Settings → Secrets and variables → Actions → Variables → New repository variable. Name: `FRONTEND_API_URL`; value: `https://YOUR_API_HOST` (HTTPS origin only, without `/api`). The workflow validates the variable, generates `dist/config.js`, and uploads it beside `index.html`. Missing or invalid values stop deployment before upload. This file is public; never add secrets. Serve it with `Cache-Control: no-store`. Changing the variable does not start deployment: run **Deploy frontend via FTP** again to publish the new URL. Each deployment overwrites any manual host edit to `config.js`.
- On the API host, set `Cors:AllowedOrigins` to the exact frontend origin (e.g. `["https://warehouse.example.com"]`, no trailing slash). Environment equivalent: `Cors__AllowedOrigins__0=https://warehouse.example.com`. Restart/redeploy the updated API: credentialed CORS now permits staff operations and the CSRF header.
- If frontend and API use different sites (for example `warehouse.example.com` and `api.other.com`), set `Authentication:CrossSiteCookies=true` (environment: `Authentication__CrossSiteCookies=true`) and restart the API. Both session and CSRF cookies remain Secure and HttpOnly; CSRF validation remains required. For HTTPS subdomains of the same site, keep the default `false`. Browser third-party-cookie restrictions can still block login on unrelated sites; use HTTPS domains under the same parent domain for reliable cookie authentication, even when servers differ.
- A reverse proxy remains optional: leave `apiBaseUrl` empty to use same-origin `/api` forwarding instead.
- The frontend build includes `public/.htaccess` for Apache/LiteSpeed SPA routing and no-store caching of `config.js`. FTP uploads this file on every deployment, replacing any host copy; keep required custom directives in the source file. Manually configured IIS `web.config` remains excluded from FTP synchronization.
- Create FTP accounts restricted to each application's own directory. Use FTPS with a valid certificate where supported.

## 2. Configure the API once

Manually create the backend's `appsettings.json` before its first start. Use the tracked example and [configuration guide](configuration.md) for required settings. Keep real credentials in host environment variables/secret storage and set `ASPNETCORE_ENVIRONMENT=Production`. Disable development seeds. Configure persistent ASP.NET Core Data Protection keys outside the deployment directory.

All `appsettings*.json` files are removed from the publish upload and excluded from FTP upload **and deletion**, preserving the server's manual configuration on every deployment. The FTP workflow does not connect to SQL. When the API restarts, it automatically applies pending EF Core migrations before accepting requests; migration failure stops startup. Configure the API SQL identity with schema migration permissions and back up the database before deployment. Production does not seed demo users. See the [migration guide](database-migrations.md).

## 3. Add GitHub secrets

Open the repository → **Settings → Secrets and variables → Actions → Secrets → New repository secret**.

| Secret | Value |
| --- | --- |
| `API_FTP_SERVER` | Backend FTP hostname, without `ftp://` |
| `API_FTP_USERNAME` | Backend FTP username |
| `API_FTP_PASSWORD` | Backend FTP password |
| `API_FTP_SERVER_DIR` | Backend application folder as seen by FTP, e.g. `/api-site/` |
| `FRONTEND_FTP_SERVER` | Frontend FTP hostname |
| `FRONTEND_FTP_USERNAME` | Frontend FTP username |
| `FRONTEND_FTP_PASSWORD` | Frontend FTP password |
| `FRONTEND_FTP_SERVER_DIR` | Frontend web root as seen by FTP, e.g. `/public_html/` |

Directory values must end with `/`. Use `/` only when the FTP account is already restricted to the exact app directory. Check both destinations before running.

Under the **Variables** tab, optionally set `API_FTP_PROTOCOL` / `FRONTEND_FTP_PROTOCOL` (default `ftps`) and `API_FTP_PORT` / `FRONTEND_FTP_PORT` (default `21`). If your host supports only plain FTP, explicitly select `ftp`; its credentials and content travel unencrypted. SFTP is a different protocol and is not supported by this action.

## 4. Deploy and verify

1. Commit the workflow files and push/merge to `main`. Both workflow files changing starts both deployments. Follow progress under **Actions**.
2. Confirm each workflow succeeds and each host contains its own built application, with no repository sources uploaded. Check the API's existing `appsettings.json` content/timestamp: it must remain unchanged.
3. Open the frontend, refresh a nested page and sign in. Expect the page to load, `/api/v1/health/live` through the frontend to succeed, and login plus authenticated mutations to work against the configured API.
4. Push a frontend-only change. Expect only the frontend workflow to run. Push an API-only change: expect only the API workflow. Push documentation-only changes: expect neither.
5. To deploy without source changes, open **Actions → Deploy API via FTP** or **Deploy frontend via FTP → Run workflow**, selecting `main`. Use this for a first deployment, retry/recovery, or after changing repository secrets/variables (those changes do not automatically start workflows).

## Operational limits

The API workflow supports IIS with the ASP.NET Core Hosting Bundle. After publishing and verifying FTP access, it uploads `/api/app_offline.htm`, waits 30 seconds for IIS to stop the application and release loaded assemblies, uploads application files, then deletes the maintenance file only after a successful upload. IIS can start the updated API on the next request. The sync action excludes the maintenance file so it remains present throughout upload. Users will temporarily receive the maintenance response; schedule deployments around warehouse activity. Even an idle running API can lock assemblies. See [Microsoft's app offline documentation](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/app-offline).

If upload fails or the workflow is canceled, the maintenance file is intentionally retained: starting a partially updated API is unsafe. Rerun **Deploy API via FTP** on `main` to complete deployment and remove the file. If automatic removal fails after a successful upload, verify the upload completed before manually deleting `/api/app_offline.htm` through FTP. A runner termination can also leave this file behind. This is a maintenance deployment, with no automatic rollback or zero-downtime guarantee. If the host overrides ANCM's `shutdownTimeLimit` above 30 seconds, increase the workflow wait beyond that value. Persistent locks after shutdown require checking the IIS app pool or other processes with the hosting provider. Other backend hosts need their own stop/restart procedure. Frontend FTP uploads remain non-atomic.

Deployments of each app are serialized without canceling an active upload. GitHub may replace older pending runs when several pushes arrive quickly; a later selected run deploys the full current app. Failed deployments need a rerun or manual dispatch, especially if subsequent changes affect only the other app. Do not delete the remote `.wros-*-ftp-state.json` files: they track synchronization. Exclusions protect API settings even during deletion; destructive clean-slate mode is disabled. The workflow adds no automated tests or seed operations. Database updates run in the API at startup.

Action reference: [FTP Deploy Action inputs and exclusions](https://github.com/SamKirkland/FTP-Deploy-Action/tree/v4.3.6).

## Manual acceptance for separate API hosting

1. Deploy the updated API and frontend. Set the GitHub `FRONTEND_API_URL` variable and API CORS origin as above. No migration or seed command is needed.
2. Reload the frontend with browser Network tools open. Expect health/session requests to target the configured API server.
3. Sign in, load a protected list, and perform an authorized edit. Expect successful credentialed requests; preflight responses allow the exact frontend origin, credentials and `X-CSRF-TOKEN`.
4. Sign out. Expect protected requests to require login again.
5. Change `FRONTEND_API_URL` to another configured API environment, rerun the frontend workflow, and reload. Expect requests to use that origin. Restore the intended production variable and deploy again.
6. Refresh a nested frontend route. Expect the SPA to load through the host's index fallback.

Owner verification of the deployed browser flow is required; builds alone do not verify hosting/CORS/cookie policy.

## Supplier stock Excel template download

The import dialog downloads the workbook through the shared API client, using the configured API origin and session cookies. Failed requests appear in the dialog instead of downloading an error page.

Build with `npm --prefix src/wros-web run build` and deploy the frontend through the existing workflow. No backend deployment, new configuration, migrations or seeds are required; keep the existing `FRONTEND_API_URL` setting.

1. Sign in with purchasing permission, open Create PO, select a supplier and open **Import supplier stock**.
2. Click **Download Excel template**. Expect `wros-supplier-stock-template.xlsx` to download. In browser Network tools, expect the template request to target the configured API origin and return HTTP 200.
3. Open the workbook in Excel. Expect supplier stock column headers, without a missing-file message or HTML content.
4. Temporarily block the template request using browser Network tools and click download again. Expect an error in the dialog and the button to become available again. Remove the block and retry; expect a successful download.

Owner verification on the production host is still required.

## Direct frontend routes return 404

React defines `/login`, but the static host must serve `index.html` for frontend paths that are not files or directories. Configure the frontend host only; do not apply SPA fallback to the API host. Existing API proxy rules, if used, must run before this fallback.

For IIS with the URL Rewrite module installed, merge this rule into the frontend `web.config` under `configuration/system.webServer/rewrite/rules`:

```xml
<rule name="WROS SPA" stopProcessing="true">
  <match url=".*" />
  <conditions logicalGrouping="MatchAll">
    <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
    <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
    <add input="{URL}" pattern="^/api(/|$)" negate="true" />
  </conditions>
  <action type="Rewrite" url="/index.html" />
</rule>
```

For Apache/LiteSpeed with mod_rewrite and `.htaccess` overrides enabled, the deployed `.htaccess` already includes these rules:

```apache
RewriteEngine On
RewriteCond %{REQUEST_URI} !^/api(/|$)
RewriteCond %{REQUEST_FILENAME} !-f
RewriteCond %{REQUEST_FILENAME} !-d
RewriteRule ^ index.html [L]
```

For Nginx, use `try_files $uri $uri/ /index.html;` in the frontend `location /` block, preserving any separate API location. Validate/reload server configuration through the hosting provider's normal process. FTP deployments preserve manually configured `web.config`; `.htaccess` is managed in `src/wros-web/public/.htaccess` and uploaded automatically.

Manual checks after the session-focus fix:
1. Open the signed-out login page. Expect one initial `/auth/me` session check (a 401 is normal). Switching away and back must not trigger more session checks while signed out. Development StrictMode may run the initial effect twice.
2. Sign in and switch away/back. Expect a session check; sign out and repeat. Expect no further focus-triggered session checks while signed out.
3. Open `/login` directly, then refresh a nested application route. Expect the React page to load instead of a hosting 404 after configuring the fallback.

No migrations, seed changes or backend deployment are required for this session-focus fix. Build with `npm --prefix src/wros-web run build`, deploy the frontend, and verify these browser checks.

## Manual acceptance for IIS deployment file locks

Changed files: `.github/workflows/deploy-api.yml` and this guide. No new secrets, application configuration, migration or seed commands are required. The existing API FTP account must be able to create and delete `/api/app_offline.htm`; `/api/` must be the IIS API application's root. Commit and push these changes to `main`, or run **Deploy API via FTP** after merging.

1. Open the running API, then start the API deployment workflow. During **Take API offline and release IIS file locks**, expect `/api/app_offline.htm` to appear in FTP and API requests to receive the maintenance response.
2. Watch **Deploy API**. Expect application files to upload without the IIS assembly-lock 550 error, while the maintenance file remains present.
3. After **Bring API online** succeeds, expect the maintenance file to be absent. Request `/api/v1/health/live`, then sign in and load a protected screen. Expect a healthy API and normal authenticated operation. Confirm server `appsettings.json` remains unchanged.
4. In a non-production environment, cancel during application upload. Expect the maintenance file to remain. Rerun the full workflow and expect a successful upload followed by maintenance-file removal and normal operation.

Please verify these checks on the IIS host; local workflow inspection cannot confirm server file-lock release or FTP permissions.
