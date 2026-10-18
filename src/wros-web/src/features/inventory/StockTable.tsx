import { DataTable } from '../../components/data-display/DataTable'
import { Badge } from '../../components/ui/Controls'
import type { InventoryRow } from './types'
export function StockTable({ items }: { items: InventoryRow[] }) {
  return (
    <DataTable
      items={items}
      getKey={(r) => r.id}
      caption="Stock lot locations"
      columns={[
        {
          key: 'product',
          label: 'Product',
          render: (r) => (
            <>
              <strong>{r.sku}</strong>
              <p>{r.product}</p>
              <small>
                {r.brand} {r.barcode}
              </small>
            </>
          ),
        },
        {
          key: 'location',
          label: 'Location / lot',
          render: (r) => (
            <div className="break-all">
              <p>{r.path}</p>
              <small>{r.stockLotId}</small>
              <p>Batch: {r.supplierBatch ?? '—'}</p>
            </div>
          ),
        },
        {
          key: 'expiry',
          label: 'Expiry / receipt',
          render: (r) => (
            <>
              <Badge
                tone={
                  r.expiry && r.expiry < new Date().toISOString().slice(0, 10)
                    ? 'danger'
                    : 'neutral'
                }
              >
                {r.expiry
                  ? `${r.expiry < new Date().toISOString().slice(0, 10) ? 'Expired' : 'Valid until'} · ${r.expiry}`
                  : 'No expiry'}
              </Badge>
              <p>{new Date(r.receivedAt).toLocaleDateString()}</p>
              <small>{r.deliveryNoteNumber}</small>
            </>
          ),
        },
        {
          key: 'quantity',
          label: 'Whole units',
          render: (r) => (
            <>
              <strong>{r.quantity.toLocaleString()} physical</strong>
              <p>{r.sellableQuantity.toLocaleString()} sellable</p>
            </>
          ),
        },
      ]}
    />
  )
}
