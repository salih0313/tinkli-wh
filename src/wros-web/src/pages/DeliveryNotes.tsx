import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { get } from '../api/client'
import type { Page } from '../api/types'
import type { ReceiptSummary } from '../features/receiving/types'
import { ErrorMessage, Heading } from '../components/Form'
import { Badge, Button, Select } from '../components/ui/Controls'
import { DataTable } from '../components/data-display/DataTable'
import { Pagination } from '../components/data-display/Pagination'
import { EmptyState, LoadingSkeleton } from '../components/feedback/States'
export default function DeliveryNotes() {
  const [data, setData] = useState<Page<ReceiptSummary> | null>(null); const [page, setPage] = useState(1)
  const [search, setSearch] = useState(''); const [query, setQuery] = useState(''); const [status, setStatus] = useState(''); const [revision, setRevision] = useState(0)
  const [loading, setLoading] = useState(true); const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError(null)
    get<Page<ReceiptSummary>>(`/api/v1/delivery-notes?page=${page}&pageSize=20&search=${encodeURIComponent(query)}&status=${status}`, controller.signal).then(setData).catch(failure => { if (!controller.signal.aborted) setError(failure) }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [page, query, status, revision])
  return <><Heading eyebrow="Receiving" title="Delivery notes" detail="Record what physically arrived, allocate each lot, then post stock atomically." action={<Link className="primary" to="/delivery-notes/new">Receive delivery</Link>} /><ErrorMessage error={error} /><section className="panel"><div className="toolbar"><form className="search-form" onSubmit={event => { event.preventDefault(); setPage(1); setQuery(search) }}><input aria-label="Search delivery notes" placeholder="DN number, supplier or reference…" maxLength={100} value={search} onChange={event => setSearch(event.target.value)} /><Button variant="secondary" type="submit">Search</Button></form><Select className="status-filter" aria-label="Receipt status" value={status} onChange={event => { setStatus(event.target.value); setPage(1) }}><option value="">All statuses</option><option>Draft</option><option>Posted</option></Select><Button variant="ghost" icon="refresh" disabled={loading} onClick={() => setRevision(value => value + 1)}>Refresh</Button></div>
    {loading ? <LoadingSkeleton label="Loading delivery notes…" /> : data?.items.length ? <DataTable items={data.items} getKey={item => item.id} caption="Delivery notes" columns={[
      { key: 'number', label: 'Delivery note', render: item => <div><strong>{item.number}</strong><p className="mt-1">{new Date(item.receivedAt).toLocaleDateString()}</p></div> },
      { key: 'supplier', label: 'Supplier / PO', render: item => <div>{item.supplierName}<p className="field-hint">{item.purchaseOrderNumber ?? 'Standalone receipt'}</p></div> },
      { key: 'status', label: 'Status', render: item => <Badge tone={item.status === 'Posted' ? 'success' : 'neutral'}>{item.status}</Badge> },
      { key: 'actions', label: 'Actions', render: item => <Link className="text-button" to={`/delivery-notes/${item.id}`}>Open<span className="sr-only"> {item.number}</span></Link> },
    ]} /> : <EmptyState title="No delivery notes found" detail="Create a receipt or adjust the search and filters." />}</section><Pagination page={page} total={data?.total ?? 0} noun="delivery notes" onPage={setPage} disabled={loading} /></>
}
