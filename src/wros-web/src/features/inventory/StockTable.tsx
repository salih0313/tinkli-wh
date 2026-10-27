import { DataTable } from '../../components/data-display/DataTable'
import { Badge } from '../../components/ui/Controls'
import type { InventoryRow } from './types'
export function StockTable({ items }: { items: InventoryRow[] }) {
  const today = new Date().toISOString().slice(0, 10)
  return <DataTable items={items} getKey={r => r.id} caption="Stock by receipt and location" columns={[
    { key: 'product', label: 'Product / SKU', render: r => <div className="inventory-stock-product"><strong>{r.product}</strong><small>{r.sku}{r.brand ? ` · ${r.brand}` : ''}</small>{r.barcode && <small>Barcode: {r.barcode}</small>}</div> },
    { key: 'location', label: 'Location / batch', render: r => <div className="inventory-stock-location"><strong>{r.path}</strong><small>Batch: {r.supplierBatch ?? 'Not provided'}</small></div> },
    { key: 'receipt', label: 'Receipt', render: r => <div className="inventory-stock-location"><strong>{r.deliveryNoteNumber}</strong><small>{new Date(r.receivedAt).toLocaleDateString()}</small></div> },
    { key: 'expiry', label: 'Expiry', render: r => <Badge tone={r.expiry && r.expiry < today ? 'danger' : 'neutral'}>{r.expiry ? `${r.expiry < today ? 'Expired' : 'Until'} · ${r.expiry}` : 'No expiry'}</Badge> },
    { key: 'quantity', label: 'On hand', render: r => <strong className="inventory-cell-number">{r.quantity.toLocaleString()}</strong> },
    { key: 'sellable', label: 'Sellable', render: r => <strong className="inventory-cell-number sellable">{r.sellableQuantity.toLocaleString()}</strong> },
  ]} />
}
