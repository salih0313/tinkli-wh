import { humanize } from '../../lib/presentation'
import { DataTable } from '../../components/data-display/DataTable'
import type { LedgerRow } from './types'
export function LedgerTable({ items }: { items: LedgerRow[] }) {
  return (
    <DataTable
      items={items}
      getKey={(r) => r.id}
      caption="Immutable stock ledger"
      columns={[
        {
          key: 'type',
          label: 'Movement',
          render: (r) => (
            <>
              <strong>{humanize(r.type)}</strong>
              <p className={r.quantity < 0 ? 'inventory-out' : 'inventory-in'}>
                {r.quantity > 0 ? '+' : ''}
                {r.quantity.toLocaleString()} units
              </p>
            </>
          ),
        },
        {
          key: 'product',
          label: 'Product',
          render: (r) => (
            <div className="break-all">
              <strong>{r.sku}</strong>
              <p>{r.product}</p>

            </div>
          ),
        },
        {
          key: 'path',
          label: 'Location / reference',
          render: (r) => (
            <>
              <p>{r.path}</p>
              <p>{r.quantity < 0 ? 'Source' : 'Destination'}</p>
              <small>{r.referenceNumber ?? r.reference}</small>
              {r.externalOrderId && <p>Order: {r.externalOrderId}</p>}
            </>
          ),
        },
        {
          key: 'time',
          label: 'Activity',
          render: (r) => (
            <div className="break-all">
              <p>{new Date(r.timestamp).toLocaleString()}</p>
              <p>{r.actor ?? (r.integrationClientId ? 'Integration' : 'Staff / system')}</p>

            </div>
          ),
        },
      ]}
    />
  )
}
