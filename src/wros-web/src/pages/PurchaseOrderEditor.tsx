import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { get, mutate } from '../api/client'
import type { PurchaseOrder, OrderLine } from '../features/purchasing/types'
import type { MasterRecord } from '../features/master-data/types'
import { useLookup } from '../hooks/useLookup'
import { ErrorMessage, Field, Heading } from '../components/Form'
import { Button, Select } from '../components/ui/Controls'
import { LoadingSkeleton } from '../components/feedback/States'
export default function PurchaseOrderEditor() {
  const { id } = useParams(); const navigate = useNavigate()
  const products = useLookup<MasterRecord>('/api/v1/products'); const suppliers = useLookup<MasterRecord>('/api/v1/suppliers')
  const [order, setOrder] = useState<PurchaseOrder | null>(null); const [lines, setLines] = useState<OrderLine[]>([{ productId: '', quantity: 1 }])
  const [loading, setLoading] = useState(!!id); const [busy, setBusy] = useState(false); const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    if (!id) return
    const controller = new AbortController()
    get<PurchaseOrder>(`/api/v1/purchase-orders/${id}`, controller.signal).then(value => { setOrder(value); setLines(value.lines) }).catch(failure => { if (!controller.signal.aborted) setError(failure) }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [id])
  function change(index: number, update: Partial<OrderLine>) { setLines(current => current.map((line, position) => position === index ? { ...line, ...update } : line)) }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = new FormData(event.currentTarget)
    if (!lines.length || lines.some(line => !line.productId || !Number.isSafeInteger(line.quantity) || line.quantity < 1)) { setError('Choose products and positive whole-unit quantities.'); return }
    setBusy(true); setError(null)
    try {
      const result = await mutate<PurchaseOrder>(id ? 'PUT' : 'POST', `/api/v1/purchase-orders${id ? `/${id}` : ''}`, { supplierId: form.get('supplierId'), orderDate: form.get('orderDate'), expectedDeliveryDate: form.get('expectedDeliveryDate') || null, remarks: form.get('remarks'), lines: lines.map(({ productId, quantity }) => ({ productId, quantity })), rowVersion: order?.rowVersion })
      navigate(`/purchase-orders/${result.id}`, { replace: true })
    } catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  return <><Heading eyebrow="Purchasing" title={id ? `Edit ${order?.number ?? 'purchase order'}` : 'Create purchase order'} detail="Whole-unit supplier requests. No approval or stock movement is created." />
    <ErrorMessage error={error || products.error || suppliers.error} />
    {loading || products.loading || suppliers.loading ? <div className="panel"><LoadingSkeleton label="Loading order form…" /></div> : id && !order ? <Link className="text-button" to="/purchase-orders">Return to purchase orders</Link> : order?.status === 'Locked' ? <p className="notice error">Receiving has started. This purchase order is locked.</p> : <form onSubmit={save} className="space-y-6" aria-busy={busy}><fieldset disabled={busy} className="space-y-6"><section className="panel p-5 sm:p-6 space-y-5"><h2 className="form-section-title">Supplier & dates</h2><div className="form-grid"><Field label="Supplier"><Select name="supplierId" required defaultValue={order?.supplierId ?? ''}><option value="">Choose supplier</option>{suppliers.items.filter(item => item.isActive || item.id === order?.supplierId).map(item => <option value={item.id} key={item.id}>{item.code} · {item.name}{!item.isActive ? ' (inactive)' : ''}</option>)}</Select></Field><Field label="PO date"><input name="orderDate" type="date" required defaultValue={order?.orderDate ?? new Date().toISOString().slice(0, 10)} /></Field><Field label="Expected delivery date (optional)"><input name="expectedDeliveryDate" type="date" defaultValue={order?.expectedDeliveryDate ?? ''} /></Field></div><Field label="Remarks"><textarea name="remarks" maxLength={2000} defaultValue={order?.remarks ?? ''} /></Field></section>
      <section className="panel p-5 sm:p-6"><div className="flex flex-wrap items-center justify-between gap-3 mb-5"><h2 className="form-section-title">Requested products</h2><Button variant="secondary" icon="plus" disabled={lines.length >= 500} onClick={() => setLines(current => [...current, { productId: '', quantity: 1 }])}>Add line</Button></div><div className="space-y-4">{lines.map((line, index) => <div key={index} className="line-editor"><div className="line-number">{index + 1}</div><Field label="Product"><Select required value={line.productId} onChange={event => change(index, { productId: event.target.value })}><option value="">Choose product</option>{products.items.filter(item => item.isActive || item.id === line.productId).map(item => <option key={item.id} value={item.id}>{item.code} · {item.name}{!item.isActive ? ' (inactive)' : ''}</option>)}</Select></Field><Field label="Quantity (units)"><input type="number" inputMode="numeric" min={1} max={Number.MAX_SAFE_INTEGER} step={1} required value={Number.isFinite(line.quantity) ? line.quantity : ''} onChange={event => change(index, { quantity: event.target.valueAsNumber })} /></Field><Button variant="danger" disabled={lines.length <= 1} onClick={() => setLines(current => current.filter((_, position) => position !== index))} aria-label={`Remove line ${index + 1}`}>Remove</Button></div>)}</div></section></fieldset>
      <div className="editor-actions"><Link className="secondary" to={id ? `/purchase-orders/${id}` : '/purchase-orders'}>Cancel</Link><Button type="submit" loading={busy}>Save purchase order</Button></div></form>}
  </>
}
