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
              <strong>{r.type}</strong>
              <p>
                {r.quantity > 0 ? '+' : ''}
                {r.quantity.toLocaleString()} units
              </p>
            </>
          ),
        },
        {
          key: 'product',
          label: 'Product / lot',
          render: (r) => (
            <div className="break-all">
              <strong>{r.sku}</strong>
              <p>{r.product}</p>
              <small>{r.stockLotId}</small>
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
              <p>{r.actor ?? r.actorId ?? r.integrationClientId ?? 'System'}</p>
              <small>{r.correlationId}</small>
            </div>
          ),
        },
      ]}
    />
  )
}
