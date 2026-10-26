# Compact purchase order editor

Changed files: src/wros-web/src/pages/PurchaseOrderEditor.tsx and src/wros-web/src/styles.css. Changes are scoped to the purchase order editor: explicit narrow code column, flexible wider product name column, compact fields/toolbars/table rows, optional mapping help on the Map button, and an opaque lavender sticky save bar.

Start the existing API and run `npm run dev` from src/wros-web. No configuration, migration or seed changes are required for this layout update.

1. Open Prepare purchase order, choose a supplier, and import the sample. At desktop width, expect a narrow code column and wider product names without single-word fragmentation. Long codes remain contained.
2. On an unmapped row, expect no mapping warning beneath the product name. Hover Map: expect “Map WROS product before saving”. Keyboard focus announces the same instruction through the button accessible name. Click Map: expect the existing mapping dialog.
3. Scroll the working list and page: expect compact rows and inputs, visible table headings, and a clearly distinct solid lavender save bar. The summary remains reachable above the save bar.
4. Change individual and common quantities, search/filter, map a product, and save: expect the existing behavior and validation.
5. At phone width, expect stacked product cards without desktop column widths. On touch devices, expect controls at least 44px tall. At tablet width, the table may scroll horizontally to preserve readable product names.

Validation: TypeScript/Vite production build and git diff whitespace check. Browser appearance and live save verification require owner review. Native Map tooltips follow browser hover behavior; touch users can tap Map directly, and keyboard users receive its accessible instruction. Please verify the layout and approve before further changes.


## Save validation, toast and date follow-up

Also changed components/feedback/States.tsx to support error toasts while preserving success toasts. Save validation distinguishes empty/oversized orders, invalid quantities and missing mappings, identifies affected codes, and offers a Map first product action. Editor errors are dismissible fixed toasts above the save bar; API correlation IDs remain visible. Browser field validation also reports through the toast. PO date and expected delivery date are grouped side by side with 164px inputs on desktop and responsive widths on phones.

1. Import unmapped products and click Save from the bottom: expect an error toast listing affected codes and a Map first product button. Click it, select the matching active WROS product and save the mapping. Repeat for remaining unmapped entries; then save the order successfully with valid quantities. Mapping remains mandatory; no name-based matching or product creation was introduced.
2. Clear a required quantity, enter zero, or enter a fraction and save: expect a visible toast identifying the quantity field. Correct it and retry. Try an empty order: expect a distinct add-products message.
3. Trigger an API save error: expect the toast visible at the bottom, with a correlation ID when supplied. Dismiss it without losing entered data. Check keyboard and mobile visibility.
4. At desktop width, expect the supplier field followed by PO date and expected delivery date together, with dates at a readable compact width. Check narrow phone widths for wrapping without page overflow.

No backend, configuration, seed or migration changes for this follow-up. Frontend production build and whitespace checks passed; live mapping/save and responsive appearance require owner verification.
