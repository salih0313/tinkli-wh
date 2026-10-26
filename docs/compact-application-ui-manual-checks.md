# Compact application UI

The purchase order editor is the density reference for all screens. Shared desktop inputs and buttons use 34px minimum height, 13px input text, 12px labels, 4px label gaps, and smaller padding. Table rows, cards, dialogs, document facts and page headers use tighter spacing. Tables size columns from their content rather than assigning 28% to every first column. PO working-list column widths remain explicit. Save bars retain the requested lavender background, purple 1px border and 5px radius.

Phone inputs use 16px text to keep entry readable and avoid focus zoom. Touch controls remain at least 44px tall, including PO quantity fields, mapping actions, warehouse tree actions and password visibility controls. Mobile table cards and horizontal table scrolling remain available.

## Files changed

All paths below are relative to src/wros-web/src:

- styles.css: shared control density, page/card/dialog/table/detail spacing, responsive touch handling and shared PO control styles.
- features/purchasing/CatalogDialogs.tsx: compact form spacing.
- pages/{Audit,DeliveryNoteDetails,DeliveryNoteEditor,Foundation,Integration,Inventory,InventoryDashboard,Login,MasterData,Password,PurchaseOrderDetails,PurchaseOrderEditor,ReceiptCorrection,Roles,StockMovements,StockTransactions,Users,WarehouseHierarchy}.tsx: tighter explicit padding/gaps in forms and cards.
- docs/compact-application-ui-manual-checks.md (repository root): this guide.

## Run and configuration

No backend, database migration, seed or configuration changes are required for this UI update. Use the existing configured API. Start it if needed:

```sh
dotnet run --project src/Wros.Api --launch-profile https
```

Start the frontend from the repository root:

```sh
cd src/wros-web
npm run dev
```

Production frontend validation: `npm run build` from src/wros-web. Deployment requires the rebuilt frontend assets.

## Owner acceptance

1. At desktop width, open Overview, Inventory overview and System status. Expect compact page headings, cards and statistics with consistent inner padding, legible text and no clipped content.
2. Open Products, Suppliers, Users and Roles. Search, paginate, open create/edit dialogs, tab through controls, expand a role and open permission controls. Expect compact forms, clearly visible labels/focus and action buttons that remain usable. Check the password visibility button aligns with its input.
3. Open PO creation and details. Expect the existing narrow code/wide product columns, grouped dates, compact quantities and lavender save bar. Import and adjust a working list; inspect the Map tooltip and an error toast. Check long names and codes for readable wrapping.
4. Open delivery note creation, lots and allocation editing, then receipt details/correction. Expect denser fields and cards with intact product, lot and rack hierarchy. On a disposable draft, edit quantities and verify normal validation; do not post physical stock solely for styling review.
5. Open Inventory explorer, Expiry alerts and Stock movements. Search/filter and inspect multi-column results, long lot IDs and rack paths. Expect compact filters and readable table content; scroll tables horizontally as needed.
6. Open supplier returns, disposal and transfer forms/detail views. Expect compact content panels and line cards, clear completion confirmations and usable quantity/location controls. Do not record real movements solely for this styling check.
7. Open Integration, Audit and Account/password. Expand details/history and view dialogs/error/loading/empty states. Expect compact spacing with intact wrapping and action visibility.
8. Repeat representative list, editor and dialog checks at 390px phone width and on a touch tablet. Expect stacked mobile records, readable 16px inputs, 44px touch targets, visible dialog close/password/tree actions, and no page-wide horizontal overflow. Wide desktop PO tables may scroll within their own container.
9. Check keyboard navigation and browser zoom at 200%. Expect visible focus, wrapping actions and accessible content. Preview printing a PO/receipt; expect readable print tables and normal document structure.

## Validation and limitations

TypeScript/Vite production build and whitespace checks passed. No automated tests or frameworks were added. Browser appearance, touch-device behavior and authenticated workflows remain for owner verification; build success does not validate those visually. Existing business rules, API contracts and print-specific table rules were not changed. Please verify and approve this styling update.
