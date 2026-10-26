# Compact application UI

> Updated workflow: PO product mapping is no longer required. Receiving automatically matches by product code or creates an internal product on DN draft save. Earlier Map-button/mapping acceptance steps below are superseded by [the current workflow and checks](purchase-order-without-mapping-manual-checks.md).

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


## Meaningful field widths

Follow-up files: components/Form.tsx and styles.css provide shared content-based widths; pages/{Integration,Inventory,StockTransactions,StockMovements}.tsx explicitly give identifiers and compound searches more room.

- Date: 164px, matching PO creation; date/time: 240px.
- Quantities/prices: 160px outside specialized table rows.
- Short codes, brand and batch fields (maximum length up to 100): 220px.
- Standard names, selects and credentials: 360px, with standard fields able to grow to 440px in shared form rows.
- Remarks, file uploads, long identifiers and explicitly wide searches: available width.

All widths respect their container. Shared form rows wrap according to field sizes; date fields stay grouped on the PO. Input heights and mobile/touch rules are retained. No schema, seed or configuration changes.

1. Check PO dates, delivery-note received date/time, and lot expiry: expect readable native pickers at compact widths rather than half-page controls. Select values and reopen the form to verify normal entry.
2. Check catalog prices/quantity, receipt units/lots, supplier batch, product code/name, and warehouse code/name: expect shorter numeric/code fields and roomier name fields. Try a large supported quantity and three-decimal price.
3. Check Inventory lot IDs, Integration WROS/external IDs and stock-out original document IDs: expect enough room for long values. Search fields and remarks keep room for longer text.
4. At 320px/390px phone widths, tablet width and 200% browser zoom, expect wrapping within the container, no clipped date calendar button, and usable touch input. Check PO dates together and receiving lots wrapping naturally.
5. Verify labels, hints, required validation and keyboard tab order still work in create/edit dialogs and receiving forms. No data validation or business rules changed.

Production build and whitespace checks pass; browser and native date-picker appearance require owner verification.


## Date-row alignment correction

Changed styles.css to reserve a two-column 340px date group (164px per date plus a 12px gap), keeping both dates on the same row beside Supplier when the panel has at least 600px available. On narrower panels the date group wraps together, preserving two usable date columns. At phone widths it fits the available width. Removed percentage-based intrinsic field sizing that allowed the nested group to collapse. Standard/wide fields in explicit responsive grids fill their allocated column; codes, numbers and dates retain meaningful limits.

1. Open PO creation on a wide desktop and confirm Supplier, PO date and Expected delivery date all align on one row. Expect no blank area above Supplier and no vertically stacked dates.
2. Resize to tablet and phone widths. Expect the date pair to stay side by side, moving together below Supplier only when there is insufficient space. Calendar buttons must remain visible.
3. Open Inventory filters, stock movement filters, receiving forms and master-data dialogs. Expect names/searches/selects to use available grid columns and short codes/quantities/dates to retain compact widths. Check wrapped labels and long values.

No configuration, seed or migration change. Run `npm run dev` or `npm run build` from src/wros-web. Frontend build and whitespace checks pass; live appearance still needs owner review.
