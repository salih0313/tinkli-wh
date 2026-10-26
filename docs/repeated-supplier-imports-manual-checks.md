# Repeated supplier availability lists

Supplier lists may repeat exactly, including identical file contents. Every upload creates its own preview/history entry containing that workbook’s rows. Confirmation updates the supplier catalog by supplier ID plus product code. Existing WROS product mappings remain intact; no display-name matching occurs. Duplicate codes within a single workbook still require correction to avoid ambiguous updates. Confirmation retries for the same import ID return the frozen prior result without applying it again.

The PO working list supports removal, quantity edits, manual additions and additions from the existing supplier catalog. Unmapped active catalog items may now be selected and mapped before saving, consistently with imported items. Confirming a repeat import in the same PO refreshes matching rows from that confirmed upload, retaining requested quantities and avoiding duplicate PO rows. Other working rows stay accessible through All PO products. Existing saved orders are unchanged unless explicitly edited or refreshed.

## Changed files

- API: Modules/Purchasing/SupplierCatalog/{SupplierCatalogService.cs,Models.cs}.
- Persistence: AllowRepeatedSupplierImports migration and designer, WrosDbContextModelSnapshot.cs.
- Web: pages/PurchaseOrderEditor.tsx, features/purchasing/CatalogDialogs.tsx.
- Documentation: this guide and phase-3.1 delivery/manual checks.

## Run

No new configuration or seed commands. Restart the updated API to apply the migration automatically before accepting requests:

```sh
dotnet run --project src/Wros.Api --launch-profile https
```

Alternatively apply explicitly with `dotnet ef database update --project src/Wros.Api`. From src/wros-web run `npm run dev`, or rebuild deployed frontend assets with `npm run build`.

## Owner acceptance

1. Upload and confirm the sample for a supplier. Upload the exact same file again: expect a new preview showing all five uploaded rows, classified as updates. Confirm successfully; expect two history entries and one catalog entry per supplier code.
2. Retry confirmation for one already confirmed import ID: expect its original result, no extra catalog updates or history entries.
3. On a fresh PO, import the unchanged file again. Expect only its rows in Current import, retained mappings, and no file-already-imported error.
4. Remove a PO row, add a different existing catalog product, and enter individual/common quantities. Expect the working list to reflect these choices. Select an unmapped active catalog product: expect it can be added, with mapping required before PO save.
5. Reimport into the same PO: expect matching entries once each, freshly confirmed details and preserved required quantities. Previously removed entries present in the new upload return as newly added rows. Switch to All PO products: expect other selected entries retained.
6. Map entries and save a PO with valid whole quantities. Reopen it and verify lines, quantities and pricing. A supplier import alone must not change physical inventory.
7. Upload different prices for the same supplier codes: expect catalog updates with stable IDs/mappings; saved PO snapshots stay unchanged. Duplicate codes within one workbook remain invalid.

## Validation and limitations

Backend/frontend build and whitespace checks; no automated tests added. Live SQL migration, import confirmation and browser save require owner verification. Rollback refuses to discard repeated import history. Catalog imports still replace supplied optional values, including blanks; product mapping remains explicit. Please verify and approve this update before further changes.
