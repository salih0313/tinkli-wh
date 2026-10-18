# Completed stock movements

StockTransaction/StockTransactionLine represent completed SupplierReturn, Disposal and Transfer records through one explicit service. SR/DS/WT year counters, stable request-key hashes, serializable transactions, row-version balances and signed append-only ledger protect stock. No pending/shipping/approval/reservation or generic adjustment workflow. Transfer preserves StockLot identity and receipt provenance; no original DN history is rewritten. Exact operation permission mappings live in PermissionCatalog. See docs/phase-6-manual-checks.md.
