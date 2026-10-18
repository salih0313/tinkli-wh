# Inventory

Read-only visibility uses InventoryQueries: paged balance projections, product SQL aggregates, dashboard totals, FIFO/FEFO and expiry filters, and immutable global ledger. Physical balances originate only from posted receiving/completed movements; zero rows are retained. Expired/inactive-product units are excluded from sellable suggestions, not physical accounting. UTC calendar expiry and whole-unit quantities are shared with integration snapshots.

InventoryBalance is row-version protected; StockMovement is append-only. Outbound and integration services own atomic balance mutations; receipt corrections retain original history. See docs/phase-5-manual-checks.md and docs/database-migrations.md.
