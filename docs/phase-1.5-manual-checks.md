# Phase 1.5 — UI redesign and manual acceptance

Phase 1.5 redesigns the existing frontend only. Phase 2 is not implemented. Existing API calls, request bodies, session handling, authorization gates, routes, password rules and database schema are preserved. Earlier Phase 1 password/seed changes already present in the working tree are separate from this redesign.

## Start locally

Use the existing Phase 1 SQL Server database, local connection configuration and accounts. This phase requires no migration, reseeding, new configuration, software or dependencies.

1. From the repository root, start the API:

   ```sh
   dotnet run --project src/Wros.Api --launch-profile https
   ```

   Expected: the API listens on `https://localhost:7243` using the existing Development settings. SQL Server must be running. Keep the existing trusted local HTTPS certificate.

2. In a second terminal, install the existing locked frontend dependencies if needed, then start Vite:

   ```sh
   cd src/wros-web
   npm ci
   npm run dev
   ```

   Expected: Vite serves `http://localhost:5173`. Open this URL; the existing same-origin proxy handles API requests and cookies.

3. Verify the production build:

   ```sh
   npm run build --prefix src/wros-web
   ```

   Run this command from the repository root. Expected: TypeScript compilation and Vite bundling succeed. `dist` is generated and remains ignored by Git. Vite preview alone does not provide the Development API proxy; use the dev server for these functional checks.

## Design and component map

- `src/wros-web/src/styles.css`: navy/blue/neutral design tokens, common controls, cards, responsive layouts, focus states and reduced-motion support. Uses local system fonts without font downloads.
- `src/wros-web/src/App.tsx`: existing route/access gates; shell delegated to a layout component and session loading presentation updated.
- `src/wros-web/src/components/layout/AppShell.tsx`: branded sidebar, desktop collapse, mobile/tablet navigation dialog, header, breadcrumbs, account dropdown and existing sign-out action.
- `src/wros-web/src/components/ui/Icon.tsx`: consistent decorative inline SVG icons.
- `src/wros-web/src/components/ui/Controls.tsx`: typed buttons, icon buttons, inputs, selects and status badges.
- `src/wros-web/src/components/ui/Card.tsx`: reusable cards and section headers.
- `src/wros-web/src/components/ui/Dialog.tsx`: native modal with focus containment, focus restoration, Escape handling and scroll locking.
- `src/wros-web/src/components/forms/PasswordInput.tsx`: password visibility toggle; password values remain in the existing forms.
- `src/wros-web/src/components/Form.tsx`: associated labels, help text, page headings and safe API error summaries.
- `src/wros-web/src/components/data-display/DataTable.tsx`: typed desktop table with a mobile card/list presentation using the same column definitions.
- `src/wros-web/src/components/data-display/Pagination.tsx`: shared pagination and disabled boundaries.
- `src/wros-web/src/components/feedback/States.tsx`: skeletons, empty states and dismissible success notifications. Notifications persist until dismissed or the existing page state clears them.
- `src/wros-web/src/hooks/useBodyScrollLock.ts`: shared overlay scroll locking, including nested confirmations and unmount cleanup.
- `src/wros-web/src/hooks/useConfirm.tsx`: accessible confirmation dialog for the existing deactivation and reset actions.
- `src/wros-web/src/lib/presentation.ts`: display labels; original role names and permission keys remain visible where relevant and API values stay unchanged.
- All seven existing pages redesigned: `pages/Login.tsx`, `Home.tsx`, `Users.tsx`, `Roles.tsx`, `Password.tsx`, `Audit.tsx`, `Foundation.tsx`.
- This checklist: `docs/phase-1.5-manual-checks.md`.

No new npm packages, UI framework, image assets, external font services, automated tests or backend changes were introduced for Phase 1.5.

## Responsive acceptance matrix

Repeat **every screen** below at 375, 768, 1024 and 1440 CSS pixels. Also check 320 pixels, a short 375 × 667 viewport, and landscape at 812 × 375 and 1024 × 768. Use browser responsive mode and repeat critical flows on a real phone/tablet where available.

| Screen/state | 375 | 768 | 1024 | 1440 | Expected visual result |
| --- | --- | --- | --- | --- | --- |
| Login | □ | □ | □ | □ | Single-column mobile form; split branded desktop layout; no keyboard-induced clipping |
| Overview | □ | □ | □ | □ | Cards stack/adapt; real role and permission data wraps; shortcuts remain usable |
| Users list and search | □ | □ | □ | □ | Mobile records below 768; readable table from 768; search and actions retain usable widths |
| Create user dialog | □ | □ | □ | □ | Labels, password toggle, role choices and actions fit; long content scrolls inside the dialog |
| Manage user / reset / confirmation | □ | □ | □ | □ | Clear account status and distinct security actions; nested confirmation stays inside viewport |
| Roles: collapsed and expanded | □ | □ | □ | □ | Role titles wrap; permission choices stack/adapt; Save remains reachable |
| Account & password | □ | □ | □ | □ | Account summary and form stack/adapt; password toggle stays at the right of each input |
| Security audit: details open | □ | □ | □ | □ | UTC times and long IDs/JSON wrap without page overflow |
| System status | □ | □ | □ | □ | Health cards stack/adapt; connection errors and reference IDs wrap |
| Navigation and account menu | □ | □ | □ | □ | Drawer on mobile/tablet; collapsible sidebar from 1024; dropdown fits viewport |

At each width, expected: no horizontal **page** scrolling, clipped actions, overlapping labels or hidden essential controls. Important controls have at least 44px touch targets. Content scrolls vertically naturally. Narrow-screen table rows become labeled cards rather than requiring sideways scrolling.

## Screen-by-screen functional checks

Use the existing `wros.admin` and `wros.staff` credentials. Never include passwords in screenshots, tickets or this document. Use a disposable staff account for account-changing checks and keep a SuperAdmin session available. Tests below deliberately exercise existing Phase 1 behavior; do not change permissions for production users.

1. **Login and password visibility**
   - Open `/login` signed out. Tab through Username, Password, Show password and Sign in. Click each field label. Use the visibility toggle and browser/password-manager autofill.
   - Expected: labels focus their input; focus is visible; toggle does not submit the form; password masking changes while its value remains intact; no registration link appears.
   - Submit empty fields, then an invalid username/password once, then valid SuperAdmin credentials.
   - Expected: browser required validation for empty fields; readable API error for invalid credentials; busy feedback during the request; successful login opens Overview (or password change when required). No authentication contract changes.
   - For lockout/rate-limiting regression, follow the existing Phase 1 checklist using a disposable account. Expected: the existing API limits and messages still apply.

2. **Authenticated shell and navigation**
   - Visit every navigation item. Refresh each URL directly. Use browser Back/Forward.
   - Expected: correct active item, header title and browser title; routes resolve without import/runtime errors; navigation returns content to the top. Only implemented pages appear.
   - At desktop widths, collapse and expand the sidebar. Hover/focus an icon while collapsed.
   - Expected: content gains space, selected icon remains visible, accessible labels/tooltips identify links. Collapse is presentation state and resets on a full reload.
   - At mobile/tablet widths, open the navigation drawer, scroll it if needed, choose a page, reopen and press Escape. Resize to desktop while the drawer is open.
   - Expected: background is inert while open; Tab stays inside the drawer; selecting a route or Escape closes it; focus returns to the menu button when applicable; desktop resize releases the overlay and body scroll.
   - Open the account menu using keyboard and pointer. Choose Account & password. Reopen, press Escape, then reopen and click outside.
   - Expected: menu closes on route change, Escape and outside click; its items are keyboard accessible and fit within the viewport.

3. **Overview**
   - Confirm the username, roles, permission count and permission list match the signed-in account.
   - Expected: figures describe actual account access; no invented inventory totals or unimplemented module links. All-warehouse scope remains unchanged.
   - Open every available workspace shortcut.
   - Expected: each goes to an existing, permitted route. Staff do not see SuperAdmin shortcuts.

4. **Users: list, search and pagination**
   - As SuperAdmin, visit `/users`, refresh, search for a known username, search for a nonexistent username, then clear the search and submit again.
   - Expected: skeleton while loading; matching results; clear empty state for no matches; results restored after clearing. Long usernames and multiple role badges wrap.
   - Use Previous/Next if more than 20 accounts exist; otherwise inspect disabled buttons.
   - Expected: correct page/count display, disabled boundaries and no duplicate submissions while loading. Mobile cards show the same username/roles/status/action as the desktop table.

5. **Create staff account and validation**
   - Click Create user. Press Escape or Cancel, reopen, and verify focus stays in the dialog while tabbing.
   - Expected: cancellation creates nothing; focus returns to Create user; the page behind the modal cannot be interacted with.
   - Enter a unique valid username, a temporary password of at least eight characters, and a staff role. Try no role selected, an invalid username and a seven-character password before submitting valid values.
   - Expected: clear validation, no invalid account created; eight characters are sufficient without composition rules. Successful creation closes the dialog, refreshes the list and shows a dismissible success notice. New user is required to change their temporary password as before.
   - Dismiss the notification.
   - Expected: notification disappears and other content remains stable.

6. **Manage account, deactivate and reactivate**
   - Open Manage on the disposable account. Change its staff role and save.
   - Expected: selected roles match current data, saving reports success, and existing session invalidation behavior is preserved.
   - Reopen, clear Active account and save. Cancel the confirmation, then repeat and confirm.
   - Expected: Cancel leaves the account active. Confirmation deactivates it, updates the status badge and ends its access. Existing backend protection against removing the last active SuperAdmin still applies.
   - Reactivate the disposable user.
   - Expected: status returns to Active; no unexpected password reset or role change.

7. **Reset password and unlock**
   - Manage the disposable user, enter a new temporary password, choose Reset password and cancel the confirmation.
   - Expected: no mutation; focus returns to the underlying editor, which remains usable.
   - Repeat and confirm. Sign in as that user.
   - Expected: existing sessions end, temporary password works, password change is required. Reset does not reactivate an inactive user or clear a lockout.
   - Lock the disposable account through the existing failed-login flow, then use Unlock account as SuperAdmin.
   - Expected: unlock action appears for locked users and clears lockout using the existing endpoint. Self-reset remains absent from the SuperAdmin’s own Manage dialog; use Account & password instead.

8. **Roles and permissions**
   - Open `/roles`, expand/collapse roles using Enter/Space and pointer, and inspect SuperAdmin.
   - Expected: all six roles are displayed with human-readable names and permission counts; original permission keys are shown; SuperAdmin permissions are fixed/disabled and have no Save action.
   - On a disposable staff role mapping, change a non-administrative permission and save, then restore the original mapping.
   - Expected: success notice and updated grants; subsequent requests use the existing permissions; no new warehouse restriction is introduced. User and role administration still require SuperAdmin regardless of assigned permission labels.

9. **Account & password**
   - Open `/password`. Check the displayed username/roles and each independently labeled field. Toggle visibility; submit mismatched confirmation, then an incorrect current password.
   - Expected: toggle never submits; mismatch remains a client validation error; incorrect current password shows an API error; form stays usable.
   - For the disposable account, submit matching valid new passwords (at least eight characters).
   - Expected: successful change signs out all sessions and returns to Login; new password works. A first-login account cannot bypass required password change through navigation or direct URLs.

10. **Security audit**
    - As an authorized user, visit `/audit`, refresh, expand an entry with change details and navigate pages.
    - Expected: actions, actor, entity reference, UTC timestamp, correlation ID and safe before/after values are readable; long content wraps. Events from the account checks appear. No password or token is displayed.

11. **System status**
    - Visit `/system` and choose Refresh status; open the API specification link.
    - Expected: checking/loading state, actual API/database statuses and reference IDs; OpenAPI opens in a new tab in Development.
    - To inspect health failure presentation, block the readiness request with browser DevTools and refresh status. Remove the block and refresh again.
    - Expected: a readable failure state rather than a fabricated healthy state; successful recovery shows actual health again.

12. **Permission-aware routes and session lifecycle**
    - Sign out using the account menu, sign back in, then sign out through the sidebar/drawer.
    - Expected: both existing logout actions work; protected URLs redirect to Login. Back/refresh cannot recover authenticated data without a session.
    - Sign in as ordinary WarehouseStaff with no required password change. Check navigation and manually enter `/users`, `/roles`, `/audit` (when audit.view is absent).
    - Expected: SuperAdmin pages/shortcuts are hidden; direct access is denied by the existing route gates and backend. Overview, account and status remain available.
    - Let the existing session expire, or remove its cookie in DevTools and perform an authenticated request.
    - Expected: the existing session logic returns to Login, with no client-side token persistence or changes to expiration policy.

13. **Feedback, keyboard, zoom and small devices**
    - Throttle the network; open each data screen. Block a data request once and retry using Refresh. Search for an empty result.
    - Expected: loading skeletons, useful errors (with reference when supplied), empty states and recovery without blank inaccessible controls. Data is never replaced by fabricated demo values.
    - Tab through every screen and dialog, use Shift+Tab, Enter, Space and Escape; try browser zoom at 200%, reduced-motion mode and a screen reader where available.
    - Expected: visible focus; labels/hints are associated; icon buttons have names; state feedback is announced; focus stays within a modal and returns after close; reduced-motion preference suppresses animation.
    - On mobile, focus every field with the on-screen keyboard open, scroll the longest editor/role panel, and rotate portrait/landscape.
    - Expected: fields remain legible (16px input text), important touch targets remain at least 44px, actions stay reachable with vertical scrolling and there is no unwanted horizontal page scroll.

## Implementation verification and limits

- Frontend production build and TypeScript compilation completed successfully during implementation.
- Browser review used the real local API/database and an isolated Chrome profile. SuperAdmin login, existing authenticated data, health responses, all six authenticated routes at 375/768/1024/1440, and responsive screenshots were inspected. Login was also reviewed at all four required widths. Staff login, restricted navigation/direct-route denial and logout were checked. Narrow/mobile dialog, drawer and dropdown checks were also performed. Create-user dialogs were reviewed at all four required widths, with additional 320px and short landscape checks. Escape cancellation was checked with a nested confirmation; only the top dialog closes, and body scrolling is restored after the final editor closes. Review created normal login/logout audit events; no account passwords or role mappings were changed. No test suite or test framework was added.
- The owner checklist above remains the acceptance gate for persistent account/permission changes, account lockout, reset, expiration and real-device/assistive-technology behavior (including Safari/VoiceOver and the physical on-screen keyboard). Visual browser review does not replace these operational checks.
- No warehouse business screen, fake operational chart, warehouse restriction, backend feature or production hosting configuration is included.
- The production build requires the existing same-origin hosting/API arrangement when deployed; deployment remains outside this phase.
- Existing password and seeding setup remains as previously configured. No migration or seed command is necessary for this UI phase.

Record browser/device, viewport, step number, actual result and a redacted screenshot for any issue. **Owner approval of Phase 1.5 is required before Phase 2 begins.**
