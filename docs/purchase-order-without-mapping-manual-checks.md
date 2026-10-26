# Purchase orders without manual product mapping

POs use supplier catalog identity (supplier plus product code), saved product labels and optional pricing. No WROS product link is needed to import, add manually, select existing catalog products, save, send, print or edit a PO. Product code and name remain required; brand, prices and supplier quantity remain optional. Existing legacy PO lines with product IDs remain supported. Historical pricing snapshots are retained until refreshed explicitly.

On saving a DN draft copied from such a PO, the server matches the supplier code to the unique WROS product code. It reuses an active exact-code product, or creates one using the supplier code/name/brand/barcode if none exists. Names and barcodes never determine identity. Existing product attributes, expiry rules and external IDs are retained; a new product uses the normal default TrackExpiry=false and no external ID. An inactive same-code product causes validation rather than duplicate creation/reactivation. Automatic matching and creation are audited within the receipt’s serializable transaction. Draft stock is unavailable until the existing posting process completes.

The other system is unchanged. Creating or linking its product remains a later activity. WROS internal product identity is distinct from that external product.

## Files changed

- API Purchasing: Entities.cs, Contracts.cs, PurchasingConfiguration.cs, PurchaseOrderService.cs.
- API Receiving: Contracts.cs, ReceivingService.cs, ReceivingProductResolution.cs.
- Persistence: OptionalPurchaseOrderProductMapping migration/designer and WrosDbContextModelSnapshot.cs.
- Frontend: features/purchasing/{types.ts,CatalogDialogs.tsx}, pages/{PurchaseOrderEditor.tsx,DeliveryNoteEditor.tsx}, features/receiving/types.ts.
- This verification guide.

## Setup

No new secrets, settings or seed data. Restart the updated API to automatically apply the migration making PO ProductId optional with a constraint requiring supplier identity plus a saved snapshot for lines without it:

```sh
dotnet run --project src/Wros.Api --launch-profile https
```

Optional explicit migration: `dotnet ef database update --project src/Wros.Api`.

From src/wros-web, start `npm run dev`, or use `npm run build` for deployment assets. Backend verification: `dotnet build src/Wros.Api --no-restore` from the repository root.

## Owner acceptance

1. Import the unchanged sample, enter required quantities and save a PO without any manual mapping. Expect success, no Map action, preserved supplier codes/names/leading zeros, optional blank prices and brand, and no newly created inventory or product records from the PO alone.
2. Add a supplier product manually with only code/name, or select an unmapped existing catalog item. Expect it can be added and the PO saved without a WROS product selector. Edit details/pricing using Edit on the working row.
3. Reopen, change quantities, send/resend and print the PO. Expect original codes/names and normal revisions/optional pricing. Existing legacy POs and mapped catalog entries remain usable. Refresh prices explicitly and check new prices apply.
4. Receive from the PO where one code already exists in Products and one does not. Expect copied code/name shown, Automatic by code selected, editable received quantities/lots/allocations, and Save draft enabled without choosing a product manually.
5. Save the DN draft. Expect the existing-code row reuses its product ID and preserves its name/expiry settings; the new-code row creates one internal product with supplier attributes and blank ExternalProductId. Inspect supplier catalog link and audit entries. Draft balances/available inventory remain unchanged.
6. Create another DN using the same codes. Expect the same internal product IDs, no duplicates, and separate receipt lots. Repeat with another supplier supplying the same code: expect code matching reuses the internal product.
7. Use an inactive matching code. Expect clear validation without creating a duplicate or reactivating it. Use an invalid/cross-supplier catalog ID, invalid quantity or invalid allocation: expect the entire save to fail with no committed automatic product/mapping changes.
8. Save an unallocated draft, reopen it, enter lots/expiry if required and complete allocations. Post only after owner-confirmed physical receiving. Expect normal posting rules, reconciled stock movements and balances, and an immediate PO lock after DN creation.
9. In two sessions receive the same previously unknown code concurrently. Expect at most one product because of unique-code constraints and serializable transactions. If one request conflicts, retry it; no partial receipt/product changes may remain.

## Validation and limitations

Backend/frontend builds and whitespace checks pass; no automated tests were added. Live SQL migration, browser receiving and concurrency scenarios require owner verification. Creation occurs on DN draft save, so saved drafts may create internal product-master records before stock posting. Unknown/new product expiry uses the existing optional-expiry default; set required expiry in Products when applicable. Supplier-code matching is global against canonical WROS codes, as requested. No auto-matching by display name and no changes to external-system source/data. Rollback refuses to invent product IDs for supplier-only POs.

Please verify and approve this update before further changes.


## Empty product ID compatibility fix

Changed pages/PurchaseOrderEditor.tsx to always submit `productId: null` for supplier catalog lines and normalize blank optional IDs. Contracts.cs applies PurchaseOrderProductIdConverter.cs only to the optional PO request product ID: legacy empty/whitespace strings become null, while malformed nonempty UUIDs remain invalid. This prevents JSON conversion errors before PO validation. No additional migration, seed or configuration is required.

Restart the updated API and rebuild/reload the frontend. Save a PO with supplier catalog rows and no internal product mapping: expect successful save. For manual API verification, submit a valid supplier line with productId null, omitted, and an empty string in separate requests: expect each to reach normal business validation and save when the rest of the request is valid. Submit a malformed nonempty UUID: expect HTTP 400. Existing legacy lines with valid product UUIDs remain supported.
