import { useEffect, useState } from 'react'
import { get } from '../api/client'
import type { Page } from '../api/types'
import type { Balance } from '../features/receiving/types'
import { ErrorMessage, Heading } from '../components/Form'
import { Button } from '../components/ui/Controls'
import { DataTable } from '../components/data-display/DataTable'
import { Pagination } from '../components/data-display/Pagination'
import { EmptyState, LoadingSkeleton } from '../components/feedback/States'
export default function ReceiptStock() {
  const [data, setData] = useState<Page<Balance> | null>(null); const [page, setPage] = useState(1); const [revision, setRevision] = useState(0)
  const [loading, setLoading] = useState(true); const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError(null)
    get<Page<Balance>>(`/api/v1/receipt-stock?page=${page}&pageSize=20`, controller.signal).then(setData).catch(failure => { if (!controller.signal.aborted) setError(failure) }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [page, revision])
  return <><Heading eyebrow="Receiving foundation" title="Receipt stock" detail="Read-only physical stock from posted receipts, by lot and placement. Use Inventory explorer for filters, FIFO and FEFO suggestions." action={<Button variant="secondary" icon="refresh" disabled={loading} onClick={() => setRevision(value => value + 1)}>Refresh</Button>} /><ErrorMessage error={error} /><section className="panel">{loading ? <LoadingSkeleton label="Loading receipt stock…" /> : data?.items.length ? <DataTable items={data.items} getKey={item => item.id} caption="Physical stock by receipt lot and location" columns={[
    { key: 'product', label: 'Product / receipt', render: item => <div><strong>{item.sku}</strong><p>{item.product}</p><small className="field-hint">{item.deliveryNoteNumber}</small></div> },
    { key: 'location', label: 'Warehouse / location', render: item => <div>{item.warehouse}<p className="field-hint">{item.path}</p></div> },
    { key: 'lot', label: 'Lot / batch / expiry', render: item => <div className="break-all"><small className="field-hint font-mono">{item.stockLotId}</small><p className="mt-2">Batch: {item.supplierBatch ?? '—'}</p><p>Expiry: {item.expiry ?? '—'}</p><p className="field-hint">Received: {new Date(item.receivedAt).toLocaleString()}</p></div> },
    { key: 'quantity', label: 'Units', render: item => <div><strong>{item.quantity.toLocaleString()} on hand</strong><p className="field-hint">{item.sellableQuantity.toLocaleString()} sellable</p></div> },
  ]} /> : <EmptyState title="No posted receipt stock" detail={error ? 'Use Refresh to retry the connection.' : 'Drafts and purchase orders do not add stock. Post a fully allocated delivery note first.'} icon="warehouse" />}</section><Pagination page={page} total={data?.total ?? 0} noun="lot placements" onPage={setPage} disabled={loading} /></>
}
