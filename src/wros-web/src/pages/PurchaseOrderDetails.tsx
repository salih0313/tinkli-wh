import { useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { get, mutate } from '../api/client'
import type { PurchaseOrder, OrderRevision } from '../features/purchasing/types'
import { ErrorMessage, Heading } from '../components/Form'
import { Badge, Button } from '../components/ui/Controls'
import { DataTable } from '../components/data-display/DataTable'
import { LoadingSkeleton, Toast } from '../components/feedback/States'
import { useSession } from '../auth/SessionContext'
import { useConfirm } from '../hooks/useConfirm'
import { PricingSummary } from '../features/purchasing/PricingSummary'
import { priceLabel, discountLabel, exactMoney } from '../features/purchasing/catalog'
export default function PurchaseOrderDetails() {
  const { user } = useSession()
  const { id } = useParams(); const { confirm, confirmation } = useConfirm()
  const [order, setOrder] = useState<PurchaseOrder | null>(null); const [history, setHistory] = useState<OrderRevision[]>([])
  const [revision, setRevision] = useState(0); const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null); const [success, setSuccess] = useState('')
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError(null)
    Promise.all([get<PurchaseOrder>(`/api/v1/purchase-orders/${id}`, controller.signal), get<OrderRevision[]>(`/api/v1/purchase-orders/${id}/revisions`, controller.signal)]).then(([value, revisions]) => { setOrder(value); setHistory(revisions) }).catch(failure => { if (!controller.signal.aborted) setError(failure) }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [id, revision])
  async function send() {
    if (!order || !await confirm('Record this purchase order as sent? Download or print the document and share it with the supplier manually. No email will be sent.')) return
    setBusy(true); setError(null)
    try { await mutate('POST', `/api/v1/purchase-orders/${id}/send`, { rowVersion: order.rowVersion }); setSuccess('Send recorded. Share the document with the supplier manually.'); setRevision(value => value + 1) } catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  async function print() {
    if (!order) return
    const popup = window.open('', '_blank')
    if (!popup) { setError('Allow pop-ups to open the printable purchase order.'); return }
    popup.opener = null
    setBusy(true); setError(null)
    try {
      const document = await mutate<{ html: string }>('POST', `/api/v1/purchase-orders/${id}/print-document`, { rowVersion: order.rowVersion })
      popup.document.open(); popup.document.write(document.html); popup.document.close()
      // Dedicated print window offers Print / Save PDF after fonts and branding images load.
      popup.focus()
    } catch (failure) { popup.close(); setError(failure) } finally { setBusy(false) }
  }
  return <><div className="no-print"><Heading eyebrow="Purchasing" title={order?.number ?? 'Purchase order'} detail="Review the request, share with your supplier and track revisions." action={<Button variant="secondary" icon="refresh" disabled={loading || busy} onClick={() => setRevision(value => value + 1)}>Refresh</Button>} /><ErrorMessage error={error} />{success && <Toast onDismiss={() => setSuccess('')}>{success}</Toast>}</div>
    {loading ? <div className="panel"><LoadingSkeleton label="Loading purchase order…" /></div> : order && <><div className="document-toolbar no-print"><Link className="secondary" to="/purchase-orders">All purchase orders</Link>{order.status !== 'Locked' && <><Link className="secondary" to={`/purchase-orders/${id}/edit`}>Edit request</Link><Button disabled={busy} onClick={() => void send()}>{order.status === 'Sent' ? 'Record resend' : 'Record send'}</Button></>}<Button variant="secondary" disabled={busy} onClick={() => void print()}>Print / Save PDF</Button>{user?.permissions.includes('receiving.manage') && <Link className="secondary" to={`/delivery-notes/new?poId=${id}`}>Receive from this PO</Link>}</div>
    <article className="panel document-sheet"><div className="document-head"><div><p className="eyebrow">WROS · Purchase order</p><h2>{order.number}</h2><p>Revision {order.revision}</p></div><Badge tone={order.status === 'Locked' ? 'warning' : 'blue'}>{order.status}</Badge></div><dl className="document-facts"><div><dt>Supplier</dt><dd>{order.supplierCode} · {order.supplierName}</dd></div><div><dt>Order date</dt><dd>{order.orderDate}</dd></div><div><dt>Expected delivery</dt><dd>{order.expectedDeliveryDate ?? 'Not specified'}</dd></div><div><dt>Last updated (UTC)</dt><dd>{new Date(order.updatedAt).toISOString()}</dd></div></dl>
      <DataTable items={order.lines} getKey={line => line.id!} caption="Requested products" columns={[{ key: 'product', label: 'SKU', render: line => line.sku }, { key: 'name', label: 'Product', render: line => line.name }, { key: 'brand', label: 'Brand / Barcode', render: line => <>{line.pricing?.brand ?? '—'}<small className="block">{line.pricing?.barcode}</small></> }, { key: 'quantity', label: 'Quantity (units)', render: line => line.quantity.toLocaleString() }, { key: 'price', label: 'Supplier price', render: line => priceLabel(line.pricing?.supplierPrice) }, { key: 'discount', label: 'Discount', render: line => discountLabel(line.pricing?.discountType, line.pricing?.discountValue) }, { key: 'net', label: 'Net unit price', render: line => priceLabel(line.pricing?.netUnitPrice) }, { key: 'total', label: 'Line total (KWD)', render: line => line.pricing?.lineTotalText ? exactMoney(line.pricing.lineTotalText) : priceLabel(line.pricing?.lineTotal) }]} /><PricingSummary saved={order.summary} />{order.remarks && <section className="document-remarks"><h3>Remarks</h3><p>{order.remarks}</p></section>}<p className="context-note">Supplier request only. Actual received quantities are recorded separately on delivery notes.</p>
    </article>
    <section className="panel mt-6 no-print"><div className="section-header"><div><h2>Revision history</h2><p>Every create, edit and recorded send is preserved.</p></div></div><div className="p-5 space-y-3">{history.map(entry => <details key={entry.revision} className="revision-entry"><summary>Revision {entry.revision} · {entry.action}<span>{new Date(entry.timestamp).toISOString()}</span></summary><p className="field-hint mt-3 break-all">Actor: {entry.actorId ?? 'System'}</p><pre className="safe-json">{JSON.stringify(JSON.parse(entry.snapshot), null, 2)}</pre></details>)}</div></section></>}{confirmation}
  </>
}
