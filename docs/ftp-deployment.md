# FTP deployment setup

Two independent workflows build and deploy on pushes to `main`. API changes deploy only the API; frontend changes deploy only the frontend; changes to both deploy both. Each workflow file also triggers its own deployment when edited. API build configuration changes trigger API deployment. Documentation-only changes deploy neither. Each selected app uploads changed build files and removes obsolete files previously tracked by its FTP state file.

## 1. Prepare the two hosts

- Backend: install the .NET 10 ASP.NET Core runtime (Hosting Bundle for IIS), configure the application/start command and HTTPS, SQL Server access and production environment. The workflow publishes a framework-dependent application; it does not provision hosting.
- Frontend: configure a static website for `dist` contents, HTTPS and SPA fallback to `index.html` for browser routes.
- **Configure a reverse proxy on the frontend host: `/api/*` must forward to the backend server, preserving the path, cookies and public request scheme/host as appropriate.** The apps run on separate servers, but the browser accesses the API through the frontend HTTPS origin. The current client uses relative URLs and same-origin cookies; simply setting a separate API URL or CORS is insufficient. Verify the hosting provider supports reverse proxying. Vite's development proxy is not part of the deployed static build. Ensure any health/dashboard API requests also use this proxy. Configure ASP.NET Core trusted proxy/forwarded-header handling for your actual topology if TLS is terminated upstream.
- Preserve your manually configured frontend `web.config` or `.htaccess`; these are excluded from FTP synchronization.
- Create FTP accounts restricted to each application's own directory. Use FTPS with a valid certificate where supported.

## 2. Configure the API once

Manually create the backend's `appsettings.json` before its first start. Use the tracked example and [configuration guide](configuration.md) for required settings. Keep real credentials in host environment variables/secret storage and set `ASPNETCORE_ENVIRONMENT=Production`. Disable development seeds. Configure persistent ASP.NET Core Data Protection keys outside the deployment directory.

All `appsettings*.json` files are removed from the publish upload and excluded from FTP upload **and deletion**, preserving the server's manual configuration on every deployment. The workflow does not run database migrations or seed users. Back up the database and apply reviewed migrations separately using the [migration guide](database-migrations.md).

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
3. Open the frontend, refresh a nested page and sign in. Expect the page to load, `/api/v1/health/live` through the frontend to succeed, and login plus authenticated mutations to work through the proxy.
4. Push a frontend-only change. Expect only the frontend workflow to run. Push an API-only change: expect only the API workflow. Push documentation-only changes: expect neither.
5. To deploy without source changes, open **Actions → Deploy API via FTP** or **Deploy frontend via FTP → Run workflow**, selecting `main`. Use this for a first deployment, retry/recovery, or after changing repository secrets/variables (those changes do not automatically start workflows).

## Operational limits

FTP uploads are not atomic: users can see mixed versions during deployment. For IIS, loaded API assemblies may be locked. Arrange an application stop/maintenance window or manually place `app_offline.htm` on the API host before uploading, then remove it after success (and handle recovery after failure). The workflow intentionally leaves that file alone. Other backend hosts need their own stop/restart procedure. Automatic maintenance/restart requires a hosting-specific mechanism and is not implemented here.

Deployments of each app are serialized without canceling an active upload. GitHub may replace older pending runs when several pushes arrive quickly; a later selected run deploys the full current app. Failed deployments need a rerun or manual dispatch, especially if subsequent changes affect only the other app. Do not delete the remote `.wros-*-ftp-state.json` files: they track synchronization. Exclusions protect API settings even during deletion; destructive clean-slate mode is disabled. No automated tests, production database updates or seed operations are added.

Action reference: [FTP Deploy Action inputs and exclusions](https://github.com/SamKirkland/FTP-Deploy-Action/tree/v4.3.6).
