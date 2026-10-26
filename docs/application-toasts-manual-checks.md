# Application-wide toast notifications

Errors and action outcomes use a shared fixed notification stack above editor save bars. It stays mounted across route navigation. Error notifications stay until dismissed; successes dismiss after eight seconds, pausing on hover or keyboard focus. Duplicate active error messages with the same reference are combined. API correlation IDs remain visible and copyable. Notifications are not persisted or logged.

The shared ErrorMessage emits toasts for page, lookup, validation and dialog failures. Native required/pattern/range validation also emits a toast and focuses the invalid field without forced scrolling. Field-specific inline hints/validation markers remain alongside the toast. Successful saves, user/role administration, receiving/posting/corrections, stock transactions, imports, price refresh, sign-in/out, password changes and print/download actions have explicit success notifications. Read-only loading states and explanatory guidance remain contextual.

The notification portal moves inside the open modal dialog so modal errors remain visible, dismissible and inside its keyboard focus boundary. It returns to the page when the dialog closes. Notifications do not appear in print output. Download feedback says Download started; it does not claim the browser completed saving a file.

## Changed files

Paths relative to src/wros-web/src:

- components/feedback/{notifications.ts,NotificationViewport.tsx}: shared store, lifecycle, accessible rendering, modal host and native validation notifications.
- main.tsx, components/Form.tsx, components/feedback/States.tsx, styles.css: shared installation, error integration and styling; previous inline Toast removed.
- api/client.ts: download-start feedback.
- components/layout/AppShell.tsx, App.tsx: sign-out and permission error integration.
- pages/{PurchaseOrderEditor,PurchaseOrderDetails,DeliveryNoteEditor,DeliveryNoteDetails,ReceiptCorrection,StockTransactions,MasterData,WarehouseHierarchy,Users,Roles,Integration,Login,Password}.tsx: action success notifications and removal of page-local success banners.
- features/purchasing/{ExcelImportDialog,CatalogDialogs}.tsx: import/preview and supplier-product feedback.
- This guide.

## Run

No backend, migration, seed or configuration changes are required. Run `npm run dev` from src/wros-web with the existing API running. For deployment, rebuild frontend assets using `npm run build` and reload the browser.

## Owner acceptance

1. At the bottom of a long PO, clear a quantity or trigger a server error and save. Expect a visible error toast above the action bar, with its reference when returned. The page should not jump to an error banner at the top. Correct the error, dismiss the toast and save; expect a success toast after navigation to details.
2. Save a product/supplier/user inside a modal with invalid data. Expect toast feedback visible above the modal, with a usable dismiss control. Open nested confirmations and close dialogs; expect the notification stack follows the open dialog and remains visible on the underlying page afterward.
3. Trigger the same error from overlapping lookups. Expect one active message per identical message/reference. Dismiss and retry: expect the failure can appear again. Trigger separate failures: expect distinct stacked notifications with scrolling if needed.
4. Save master data, PO and DN drafts, permissions and integration settings. Expect exactly one relevant success message for each successful action, including repeated saves returning the same text. Import/validate/confirm supplier spreadsheets and refresh PO prices: expect contextual feedback. Check no duplicate inline success banner remains.
5. In owner-approved scenarios, post a receipt, correct allocations or record a stock movement. Expect a success message only after API success. Invalid/failed requests must show error toasts instead. Existing confirmations remain required.
6. Check login failures, sign-in/out, password confirmation mismatch and session/network failures. Expect toast feedback. Change a password in a disposable account: expect the success message survives the redirect to sign-in.
7. Hover/focus a success toast for more than eight seconds: expect it stays while interacting. Move away: expect it dismisses after the delay. Error toasts remain until dismissed. Use keyboard and a screen reader: errors use alert, successes use status, dismiss controls have labels and focus is not stolen by new messages.
8. At phone/tablet widths and after scrolling inside a dialog, check toast text/reference wrapping and dismiss touch targets. Print a document: expect no notification overlay in output. Start a template download: expect Download started, not a claim of completed delivery.

## Verification and limits

TypeScript/Vite production build and whitespace checks pass. No automated tests or frameworks were added. Authenticated browser, modal focus, screen-reader and touch-device behavior require owner verification. Route-local action/error banners were replaced; persistent record statuses and guidance remain part of their screens. Confirm the manual scenarios and approve this change.
