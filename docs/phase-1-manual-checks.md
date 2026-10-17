    # Phase 1 manual acceptance — Identity and access

Phase 0 was approved. This checklist covers Phase 1 only. No automated tests, test frameworks, browser automation or CI were added. Complete these owner-operated checks before explicitly approving Phase 2. Never send passwords, cookies, CSRF tokens, connection strings or User Secrets output in test reports; report the action, HTTP status and correlation ID.

## Setup

1. **Restore and build.** From the repository root:
   ```sh
   dotnet restore Wros.slnx --locked-mode
   dotnet tool restore
   dotnet build Wros.slnx --no-restore
   npm ci --prefix src/wros-web
   npm run build --prefix src/wros-web
   ```
   Expected: .NET builds with zero warnings/errors; TypeScript and Vite build the SPA. No automated test command runs. Required software and secure connection setup are in README.

2. **Set passwords in the local C# file and apply migrations.** Edit `src/Wros.Api/Modules/Identity/DevelopmentSeedCredentials.local.cs`: enter `SuperAdminPassword` and `WarehouseStaffPassword`. If missing, copy DevelopmentSeedCredentials.example.cs to that filename. The local file is ignored by Git; keep the example empty. Use at least 8 characters. No seed flags, appsettings values or password environment variables are required. Then run:
   ```sh
   dotnet ef database update --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
   dotnet ef migrations list --project src/Wros.Api --startup-project src/Wros.Api -- --environment Development
   ```
   Expected: schema/role migrations apply without resetting the database. EF's DevelopmentUserSeed.cs callback then creates missing wros.admin (SuperAdmin) and wros.staff (WarehouseStaff), using Identity password hashing, role memberships and safe audit records in one transaction. It also runs when all migrations were already applied. Missing/weak passwords report the local C# file to edit. Normal database update builds your edited file; do not use --no-build after editing it.

3. **Verify file-based seeding is repeatable and Development-only.** Run database update again. Change a local seed password and run again.
   Expected: no duplicate users, grants or audit entries; existing passwords/activation/memberships stay unchanged. Use authenticated password management to change an existing account's password. The callback creates only missing usernames, with no opt-in flag. Outside Development, the callback is not registered and never creates demo accounts. Do not reset the database or alter migration history. Keep the local password file out of Git; the blank example and seed logic are tracked. Production scripts do not execute EF seeding callbacks.

4. **Start the current API and restart Vite.** Stop the previously running Phase 0 API/Vite instances first; do not run two WROS instances on the same localhost hostname while testing cookies.
   ```sh
   dotnet run --project src/Wros.Api --launch-profile https
   ```
   In another terminal:
   ```sh
   npm run dev --prefix src/wros-web
   ```
   Open `http://localhost:5173`. Expected: unauthenticated access routes to Login. The local Vite proxy forwards /api and /openapi to HTTPS 7243. No VITE_API_BASE_URL override is needed or consumed. Confirm `https://localhost:7243/api/v1/health/ready` is Healthy after migrations. VS Code F5 using WROS API (Development) is an alternative to the first terminal command.

## Authentication and sessions

5. **SuperAdmin login.** Sign in as `wros.admin` with the password set in DevelopmentSeedCredentials.local.cs before its initial creation.
   Expected: Overview displays the account, SuperAdmin role and all twelve permissions. Navigation includes Users, Roles, Security audit, System status and Change password. System status shows Healthy for process/database. Login succeeds without storing tokens/passwords in localStorage/sessionStorage. In browser application/network tools, the session cookie is __Host-Wros.Session with Secure, HttpOnly, SameSite=Strict and Path=/; its contents must not be copied into reports. API responses include X-Correlation-ID. Sessions have no sliding renewal.

6. **Staff login and protected routes.** Use a separate private browser window (separate cookie storage) and sign in as `wros.staff`.
   Expected: WarehouseStaff and inventory.view only. Users, Roles and Security audit navigation are hidden. Direct navigation to /users, /roles or /audit displays an access-denied message. Overview, System status and Change password work. In the staff window's browser console:
   ```js
   console.log((await fetch('/api/v1/users')).status)
   console.log((await fetch('/api/v1/roles')).status)
   console.log((await fetch('/api/v1/audit')).status)
   ```
   Expected: 403 for each, even with direct API requests. The application takes the actor from the validated session, not a supplied user ID.

7. **Invalid login, generic errors and lockout.** Use a throwaway staff account created in step 10 rather than your only administrator. Sign it out, then attempt an unknown username, a wrong password, and an inactive/locked account.
   Expected: identical generic invalid-credentials/account-unavailable response (401), no account-existence details. A failed login remains on Login with an accessible error and correlation reference. For the throwaway active account, enter the wrong password five times; then try its correct password.
   Expected: correct credentials are rejected while locked for fifteen minutes. SuperAdmin Users shows Locked out. Either wait fifteen minutes or use Manage → Unlock account, then sign in successfully with the correct password. Unlock does not activate an inactive account or reset its password. Passwords are never shown in audit data or logs. Lockout applies to new accounts by default.

8. **Login throttling.** In a disposable account/session, submit repeated login requests quickly, each through the normal form/CSRF flow. Avoid locking your administrator account; use a nonexistent username for this check.
   Expected: after fifteen login requests from the same IP in one minute, further requests return 429 with a retry message. Wait for the next minute window. Password-change/reset/create/unlock operations are limited to ten per user per minute. Vite proxies local browsers through one local IP, so local login rate limits are shared.

9. **Unauthenticated API and CSRF enforcement.** In a browser window with no WROS session:
   ```js
   console.log((await fetch('/api/v1/auth/me')).status)
   console.log((await fetch('/api/v1/users')).status)
   ```
   Expected: 401 ProblemDetails, not an HTML login redirect. Public health/system endpoints still return 200. POST login with no CSRF token:
   ```js
   const response = await fetch('/api/v1/auth/login', {
     method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}'
   });
   console.log(response.status);
   ```
   Expected: 400 safe CSRF ProblemDetails, no login attempt or cookie issuance. In the administrator window, POST /api/v1/auth/logout without X-CSRF-TOKEN also returns 400 and leaves the session usable. GET /api/v1/auth/csrf returns a request token and an HttpOnly Secure SameSite=Strict antiforgery cookie; the typed client does this automatically. Never print or share its token. No state-changing endpoint accepts GET.

## User administration

10. **Create a staff account and validate input.** As SuperAdmin, open Users → Create user. Enter a unique username such as `phase1.operator`, a strong temporary password, and WarehouseStaff. Save.
    Expected: success message, one account in the list, Active with Password change required. Assignments persist. Log into that new account in the private window. It is routed to Change password; API attempts outside /auth/me, /auth/csrf, /auth/change-password and /auth/logout return 403 until changed.

    In SuperAdmin, attempt the same username again (including different letter casing), a weak password, and an account without any role. Expected: duplicate is rejected (409), weak password produces clear policy feedback, and missing roles are rejected. A failed creation leaves no partial user/role/audit mutation. Usernames accept 3–64 letters/numbers or . _ @ + -; no email address or other PII is required.

11. **Self-service password change.** As the new account, try mismatched confirmation, an incorrect current password, and a weak new password. Confirm a seven-character new password is rejected and an eight-character password is accepted, including passwords containing only digits or repeated characters. Then supply its correct current password and a valid matching new password.
    Expected: invalid attempts show clear feedback without a successful audit entry or partial state change. Successful change clears the forced-change flag and signs out all sessions. Old password fails; new password succeeds and opens Overview. Password fields are cleared after success. Repeat from an ordinary seeded account if desired; it has the same self-service flow and no user ID input. Admins use this same flow for their own password.

12. **Activation, role assignment and revocation.** Keep the new staff account logged in privately. As SuperAdmin, refresh Users, manage that account, uncheck Active, confirm and save.
    Expected: deactivation persists and is audited with safe before/after state. The next private-window API request returns 401 and clears the session; login with the correct password is rejected while inactive. Reactivate it, select an additional defined role, save, and sign in again. Expected: both assigned roles are visible and permission grants are their union. Role reassignment invalidates prior sessions. Attempt to deactivate your current SuperAdmin or remove its own SuperAdmin role: a clear error prevents the operation. Managing another account never changes the actor identity to that target ID.

13. **Admin password reset and unlock.** As SuperAdmin, manage the new staff account and use Reset password with a strong temporary password. Confirm the reset.
    Expected: successful audited reset, all previous user sessions become invalid, old password fails, temporary password succeeds only into forced Change password. Reset tokens are generated and consumed server-side; no reset link/token is returned to the browser. Reset does not silently activate or unlock the account; use those explicit actions as needed. A normal staff session cannot call the reset/unlock API (403). The admin cannot use this reset form on their own account; use Change password instead.

14. **Concurrency handling.** Open Users in two admin tabs. Load the same target account in both. Save an activation/role edit in tab A, then save the stale form in tab B without refreshing.
    Expected: tab B returns 409 with a refresh message and does not overwrite tab A. Repeat with the same role-permission form in two tabs. Expected: one update commits, the stale one conflicts, with no partial grant replacement. Refresh users/roles using the provided button before retrying. Do not use administrative forms whose permissions data could be out of date without reloading after a 409.

## Permissions and auditing

15. **Live permission grants and SuperAdmin boundaries.** As SuperAdmin, open Roles. Confirm all six role names and the initial grant table in README. SuperAdmin grants are full/read-only. Add audit.view to WarehouseStaff and save.
    Expected: staff's next GET /api/v1/audit succeeds (200) without signing in again; re-focus the private browser window to refresh its navigation and see Security audit. Remove audit.view: the next API request returns 403, and refreshed navigation hides it. Grant users.manage and roles.manage to WarehouseStaff temporarily. Expected: staff still receives 403 for /api/v1/users and /api/v1/roles because SuperAdmin is additionally required; staff cannot escalate by calling these APIs. Remove those temporary grants after checking.

    Rerun database update. Expected: no duplicate roles/permissions or password changes, and staff role edits remain exactly as you saved them. Restore WarehouseStaff to inventory.view only when finished. Permissions for later modules are catalog entries only; no later-phase warehouse endpoint is implemented merely because its permission exists.

16. **Security audit.** As SuperAdmin, open Security audit → Refresh audit. Examine events from preceding steps. Use the API's paginated responses to page through entries if needed.
    Expected: role/user seeding, successful/failed/locked login, access denied/authentication required, user creation/update, password change/admin reset/unlock and logout events appear. Mutations record actor UUID, action, entity/target UUID, UTC timestamp, safe old/new values where appropriate, and correlation ID. The actor is the admin for staff-management actions; login success uses the password-validated user; anonymous/system operations have a null actor. Migration seed correlation is migration-seed (older standalone seed entries retain system-seed); HTTP actions match response X-Correlation-ID. Audit has no edit/delete endpoint. No passwords, password hashes, security stamps, authentication/CSRF/reset tokens, full connection strings or request bodies appear in old/new data or structured logs. Permission edits and account edits commit audit entries in the same transaction. The audit list is deterministic and paginated; invalid page/pageSize values are rejected.

## Session lifecycle, recovery and UI

17. **Logout and session revocation.** Log the same test account into separate normal/private cookie stores. Sign out from one.
    Expected: logout succeeds through the CSRF-protected POST, clears that browser session and invalidates all sessions for that account. The next request in the other store returns 401. Browser Back and direct protected URLs do not restore access. Logout while disconnected displays an error; it does not falsely claim server revocation succeeded.

18. **Session expiry.** Stop the API and restart temporarily with a one-minute session:
    ```sh
    dotnet run --project src/Wros.Api --launch-profile https -- --Authentication:SessionMinutes=1
    ```
    Sign in afresh, then wait more than one minute (including while actively navigating).
    Expected: no sliding extension; frontend returns to Login by the reported expiry and protected API requests return 401. Sign-in works again. Restore the normal launch command afterwards (default 15 minutes). An inactive/locked user or changed security stamp is rejected before expiry too.

19. **Loading, failures, empty states and responsive access.** Search Users for a username that does not exist. Stop the API and try refresh/search/save, then restart it and retry. Resize the browser to 375px and 768px. Navigate Login, Overview, Users, Roles, Security audit and Change password using Tab/Enter/Space. Use a keyboard on checkboxes and the refresh buttons.
    Expected: loading states and disabled submit buttons prevent accidental repeats; empty search is explicit; network failures show useful retry feedback, not a blank page. Authentication errors do not expose credentials. Duplicate/stale edits are not silently accepted. Tables scroll within their panel on narrow screens; page content remains usable without clipped forms. Labels, fieldsets, focus outlines, skip link, alerts and success/live status messages are accessible. Password inputs use appropriate autocomplete. No unnecessary animations or additional UI framework is present.

20. **Environment and configuration safeguards.** No production deployment is required for acceptance. Review the Options and migration guards and, if desired, run locally with invalid values such as SessionMinutes=0, an empty database connection, or Seed:SampleDataEnabled=true.
    Expected: clear startup/seed errors; no database reset or automatic migration. The legacy Seed:Enabled flag stays false and no longer controls account seeding. Demo-user callbacks are only registered in Development. No seed environment variables are required. Development diagnostics and OpenAPI remain unavailable outside Development. Data Protection uses local default key storage; production key encryption/persistence, account bootstrap and hosting remain deployment decisions, not implemented production infrastructure.

## Current file-based seed update — 2026-10-16

The owner requested removing the opt-in flag and using passwords directly from a file. DevelopmentUserSeed.cs now runs through EF UseSeeding/UseAsyncSeeding during database update, even when all migrations are applied. Passwords are edited in DevelopmentSeedCredentials.local.cs, which is ignored by Git; the tracked example is empty. Previously pushed source password literals had been removed, so local fields start empty and need to be filled by the owner. The old user-seeding branches/guards in data migrations are compatibility markers; schema/role operations and migration IDs remain. No database reset or password reset is performed.

Earlier verification confirmed the database TinkliWH at localhost,1433 exists with Identity tables, six roles and twelve permissions. Authenticated/browser acceptance remains owner-operated. The new file-based flow requires the owner to enter passwords before its first user creation; builds alone do not prove login or seeding acceptance.

## Owner gate

Report numbered pass/fail results and safe correlation references. Phase 1 needs explicit owner approval before Phase 2 begins.
