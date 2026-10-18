# Warehouse operator guide

All quantities are whole units. Never enter physical inventory directly into SQL or a balance editor. WROS has no warehouse checkout reservations.

1. **Prepare master data.** Active supplier/product, warehouse > area > rack > optional location. Use SKU/stable identifiers, not name matching. Expiry tracking controls mandatory expiry when posting future receipts.
2. **Purchase order.** Draft, review, record send and share printable/PDF document manually. Edit/resend with revisions until any linked DN is created, which locks the PO. Ordered quantity does not create stock or require complete fulfillment.
3. **Receive.** Copy PO as an editable starting point or create standalone DN. Enter actual quantities; remove unreceived lines, add substitutes, split lots and rack positions. Batch optional; expiry required for expiry-tracked products. Save draft without stock impact. Verify physical receipt and exact allocation sums, then confirm Post. Posting is irreversible stock-in with traceable actor/correlation.
4. **Rack correction.** On a posted DN, correct only available original stock placements; preserve original received total and each warehouse total. Already consumed/moved stock cannot be rewritten. Use completed Warehouse transfers for interwarehouse movement. Original allocations remain immutable history.
5. **Review availability.** Inventory overview shows physical/sellable/expired whole units. Explorer searches SKU/name/barcode/brand, batch, hierarchy and lot; product grouping expands locations. FIFO/FEFO sort selects sellable recommendations. Expiry filters retain expired physical units. Expiry date today is valid until UTC midnight. Recommendations never reserve stock; choose actual picked placements and recheck availability.
6. **Return to supplier.** After physical handover, choose source lot/placement and actual positive units, supplier, optional original posted DN/PO reference and remarks. Confirm completion and submit. SR-year-number is final; source units leave inventory and remain in signed history.
7. **Dispose damaged/expired stock.** After physical disposal, choose exact stock and reason. Confirm completion. DS-year-number records the final deduction. Expired physical units can be disposed/returned; an inactive product must be reactivated by authorized staff before physical stock operations.
8. **Transfer warehouses.** Move goods physically first. Record source lot/placement, destination rack/optional position in another warehouse and actual units. Add source selections to split across destinations. Confirm completion. WT-year-number atomically reduces source and adds destination, preserving lot/provenance/company total. No approval/shipping/receiving process.
9. **Trace history.** Stock movements show signed source (negative) or destination (positive) locations, reference number, actor/client and correlation ID. Search a transfer reference to see both sides; their net equals zero. Transaction details are read-only. Audit viewer is separately permission-protected.
10. **E-commerce.** Physically pick/pack chosen lots, then e-commerce calls Ready to Deliver with exact allocations. Only successful WROS deduction permits e-commerce finalization. Review integration history/order/correlation for failures. Never send a new identity to bypass an unknown result.

## Errors and safe recovery

- Validation 400/422: correct active identifiers, integer quantities, expiry, allocation sums or mapping; no partial stock transaction is committed.
- Conflict 409: refresh source availability/document version; another operator may have changed stock. Do not force retry stale data.
- Network error after submit: keep the same form/key open, check transaction history and retry the unchanged request. Closing/recreating a form creates a new key; reconcile before doing so.
- Integration timeout: retain exact event/body, fresh assertion per retry, wait for confirmed original success. Do not finalize Ready to Deliver on an unknown outcome.
- Mistaken completed transaction: escalate to owner for an approved physical corrective process; editing/deleting ledger rows or posting imaginary stock is never appropriate. No generic transaction reversal UI is included.

Touch and keyboard: important controls are at least 44px; use sidebar drawer on tablet/mobile, labeled cards for tables and visible focus. Use normal browser search/select keyboard interaction. Manual screen checks are documented per phase.
