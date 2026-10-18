import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { get } from '../api/client'
import type { Page } from '../api/types'
import type { PurchaseOrder } from '../features/purchasing/types'
import { ErrorMessage, Heading } from '../components/Form'
import { Badge, Button, Select } from '../components/ui/Controls'
import { DataTable } from '../components/data-display/DataTable'
import { Pagination } from '../components/data-display/Pagination'
import { EmptyState, LoadingSkeleton } from '../components/feedback/States'
export default function PurchaseOrders() {
  const [data, setData] = useState<Page<PurchaseOrder> | null>(null)
  const [page, setPage] = useState(1); const [search, setSearch] = useState(''); const [query, setQuery] = useState(''); const [status, setStatus] = useState('')
  const [loading, setLoading] = useState(true); const [error, setError] = useState<unknown>(null); const [revision, setRevision] = useState(0)
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError(null)
    get<Page<PurchaseOrder>>(`/api/v1/purchase-orders?page=${page}&pageSize=20&search=${encodeURIComponent(query)}&status=${status}`, controller.signal)
      .then(setData).catch(failure => { if (!controller.signal.aborted) setError(failure) }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [page, query, status, revision])
  return <><Heading eyebrow="Purchasing" title="Purchase orders" detail="Supplier requests and revision history. Purchase orders never add stock." action={<Link className="primary" to="/purchase-orders/new">Create purchase order</Link>} />
    <ErrorMessage error={error} /><section className="panel"><div className="toolbar"><form className="search-form" onSubmit={event => { event.preventDefault(); setPage(1); setQuery(search) }}><input aria-label="Search purchase orders" placeholder="PO number or supplier…" value={search} maxLength={100} onChange={event => setSearch(event.target.value)} /><Button variant="secondary" type="submit">Search</Button></form><Select className="status-filter" aria-label="Purchase order status" value={status} onChange={event => { setStatus(event.target.value); setPage(1) }}><option value="">All statuses</option>{['Draft', 'Sent', 'Locked'].map(value => <option key={value}>{value}</option>)}</Select><Button variant="ghost" icon="refresh" disabled={loading} onClick={() => setRevision(value => value + 1)}>Refresh</Button></div>
    {loading ? <LoadingSkeleton label="Loading purchase orders…" /> : data?.items.length ? <DataTable items={data.items} getKey={value => value.id} caption="Purchase orders" columns={[
      { key: 'number', label: 'Purchase order', render: value => <div><strong>{value.number}</strong><p className="mt-1">{value.orderDate}</p></div> },
      { key: 'supplier', label: 'Supplier', render: value => value.supplierName },
      { key: 'status', label: 'Status', render: value => <Badge tone={value.status === 'Draft' ? 'neutral' : value.status === 'Locked' ? 'warning' : 'blue'}>{value.status}</Badge> },
      { key: 'actions', label: 'Actions', render: value => <Link className="text-button" to={`/purchase-orders/${value.id}`}>Open<span className="sr-only"> {value.number}</span></Link> },
    ]} /> : <EmptyState title="No purchase orders found" detail="Create a supplier request or adjust your filters." />}</section><Pagination page={page} total={data?.total ?? 0} noun="purchase orders" onPage={setPage} disabled={loading} />
  </>
}
