# Internal ID display cleanup

Changes cover inventory and ledger tables, receiving details/corrections, stock transactions, supplier import history, purchase-order revisions, audit history, integration status, notifications, and shared history formatting. Receipt allocation validation now refers to a numbered lot instead of its UUID.

No configuration, migration or seed changes are required. Start the API with `dotnet run --project src/Wros.Api --launch-profile https` and frontend with `npm --prefix src/wros-web run dev`.

1. Open inventory, receipt stock, stock movements and a posted DN. Expect product codes, quantities, batch/expiry information and location paths without internal UUID subtitles. DN lots have sequential labels.
2. Open a receipt correction and a supplier-return, disposal or transfer. Expect readable locations and quantities; selection, saving and links must still work.
3. Open supplier import history, PO revision history and audit history. Expect no internal entity, actor, correlation or concurrency identifiers in visible change details; business values remain visible.
4. Open integration status. Expect client names and no snapshot/correlation UUID subtitles. External business references remain available.
5. Post an underallocated DN. Expect the actionable validation message with a lot number, not a UUID, and no technical reference subtitle in the notification. Correct allocations and retry successfully.

Limitations: this changes presentation only. Internal IDs remain in API data, links and stored audit records. Names are shown where already supplied; standalone technical actor fields are omitted where the API supplies only an ID. Live UI verification requires the owner's configured database and login.

Please verify these scenarios and approve the UI change.
