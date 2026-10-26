import { notifySuccess } from '../components/feedback/notifications'
import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom'
import { get, mutate } from '../api/client'
import type { PurchaseOrder } from '../features/purchasing/types'
import type { MasterRecord } from '../features/master-data/types'
import type { DraftLine, DraftLot, Placement, Receipt, ReceiptOrderLookup } from '../features/receiving/types'
import { emptyLot, formatUnits, localDateTime } from '../features/receiving/types'
import { AllocationEditor } from '../features/receiving/AllocationEditor'
import { useLookup } from '../hooks/useLookup'
import { useConfirm } from '../hooks/useConfirm'
import { useSession } from '../auth/SessionContext'
import { ErrorMessage, Field, Heading } from '../components/Form'
import { Button, Select } from '../components/ui/Controls'
import { EmptyState, LoadingSkeleton } from '../components/feedback/States'

interface Draft { supplierId: string; purchaseOrderId: string; supplierReference: string; receivedAt: string; remarks: string; lines: DraftLine[] }
export default function DeliveryNoteEditor() {
  const { id } = useParams(); const [params] = useSearchParams(); const initialPoId = params.get('poId'); const navigate = useNavigate()
  const { user } = useSession(); const { confirm, confirmation } = useConfirm()
  const products = useLookup<MasterRecord>('/api/v1/products'); const suppliers = useLookup<MasterRecord>('/api/v1/suppliers')
  const placements = useLookup<Placement>('/api/v1/delivery-notes/placements'); const orders = useLookup<ReceiptOrderLookup>('/api/v1/delivery-notes/purchase-orders')
  const [original, setOriginal] = useState<Receipt | null>(null)
  const [draft, setDraft] = useState<Draft>({ supplierId: '', purchaseOrderId: '', supplierReference: '', receivedAt: localDateTime(new Date().toISOString()), remarks: '', lines: [{ productId: '', quantity: 0, lots: [emptyLot()] }] })
  const [loading, setLoading] = useState(!!id); const [busy, setBusy] = useState(false); const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    if (!id) return
    const controller = new AbortController()
    get<Receipt>(`/api/v1/delivery-notes/${id}`, controller.signal).then(value => {
      setOriginal(value); setDraft({ supplierId: value.supplierId, purchaseOrderId: value.purchaseOrderId ?? '', supplierReference: value.supplierReference ?? '', receivedAt: localDateTime(value.receivedAt), remarks: value.remarks ?? '',
        lines: value.lines.map(line => ({ productId: line.productId, supplierCode: line.sku, supplierName: line.name, quantity: line.quantity, lots: line.lots.map(lot => ({ supplierBatch: lot.supplierBatch ?? '', expiry: lot.expiry ?? '', quantity: lot.quantity, allocations: lot.allocations })) })) })
    }).catch(failure => { if (!controller.signal.aborted) setError(failure) }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [id])
  useEffect(() => {
    const controller = new AbortController()
    if (!id && initialPoId) void copyPurchaseOrder(initialPoId, true, controller.signal)
    else if (!id) setBusy(false)
    return () => controller.abort()
    // Initial route source is copied once; later source changes require explicit confirmation.
  }, [id, initialPoId])
  async function copyPurchaseOrder(poId: string, initial = false, signal?: AbortSignal) {
    if (!poId) { setDraft(value => ({ ...value, purchaseOrderId: '' })); return }
    if (!initial && draft.lines.some(line => line.productId || line.supplierProductId) && !await confirm('Copy this PO and replace the current unsaved product lines? Actual quantities and allocations remain editable.')) return
    setBusy(true); setError(null)
    try {
      const po = await get<PurchaseOrder>(`/api/v1/purchase-orders/${poId}`, signal)
      if (signal?.aborted) return
      setDraft(value => ({ ...value, supplierId: po.supplierId, purchaseOrderId: po.id,
        lines: po.lines.map(line => ({ productId: line.productId ?? '', supplierProductId: line.productId ? null : line.supplierProductId, supplierCode: line.sku, supplierName: line.name, quantity: line.quantity, lots: [emptyLot(line.quantity)] })) }))
    } catch (failure) { if (!signal?.aborted) setError(failure) } finally { if (!signal?.aborted) setBusy(false) }
  }
  function lineChange(index: number, values: Partial<DraftLine>) { setDraft(value => ({ ...value, lines: value.lines.map((line, position) => position === index ? { ...line, ...values } : line) })) }
  function lotChange(lineIndex: number, lotIndex: number, values: Partial<DraftLot>) { lineChange(lineIndex, { lots: draft.lines[lineIndex].lots.map((lot, position) => position === lotIndex ? { ...lot, ...values } : lot) }) }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (draft.lines.some(line => (!line.productId && !line.supplierProductId) || !Number.isSafeInteger(line.quantity) || line.quantity < 0 || line.lots.some(lot => !Number.isSafeInteger(lot.quantity) || lot.quantity < 0 || lot.allocations.some(a => !a.rackId || !Number.isSafeInteger(a.quantity) || a.quantity < 1)))) { setError('Choose valid products/locations and whole-unit quantities. Received and lot quantities may be zero; allocation entries must be positive.'); return }
    setBusy(true); setError(null)
    try {
      const result = await mutate<Receipt>(id ? 'PUT' : 'POST', `/api/v1/delivery-notes${id ? `/${id}` : ''}`, { ...draft, purchaseOrderId: draft.purchaseOrderId || null, receivedAt: new Date(draft.receivedAt).toISOString(), rowVersion: original?.rowVersion,
        lines: draft.lines.map(line => ({ productId: line.productId || null, supplierProductId: line.supplierProductId || null, quantity: line.quantity, lots: line.lots.map(lot => ({ ...lot, supplierBatch: lot.supplierBatch || null, expiry: lot.expiry || null, allocations: lot.allocations.map(({ rackId, locationId, quantity }) => ({ rackId, locationId, quantity })) })) })) })
      notifySuccess('Delivery note draft saved.'); navigate(`/delivery-notes/${result.id}`, { replace: true })
    } catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  const choicesLoading = products.loading || suppliers.loading || placements.loading || orders.loading
  return <><Heading eyebrow="Receiving" title={id ? `Edit ${original?.number ?? 'delivery note'}` : 'Receive a delivery'} detail="Record actual items and quantities. Drafts do not affect stock; posting requires fully matched lot allocations." />
    <ErrorMessage error={error || products.error || suppliers.error || placements.error || orders.error} />
    {loading || choicesLoading ? <div className="panel"><LoadingSkeleton label="Loading receiving form…" /></div> : id && !original ? <Link className="text-button" to="/delivery-notes">Return to delivery notes</Link> : original?.status === 'Posted' ? <ErrorMessage error="This receipt is posted. Open its details to correct allocations." /> : <form onSubmit={save} className="space-y-3" aria-busy={busy}><fieldset disabled={busy} className="space-y-3">
      <section className="panel p-3 space-y-3"><h2 className="form-section-title">Delivery details</h2><Field label="Start from purchase order (optional)"><Select disabled={!!id} value={draft.purchaseOrderId} onChange={event => void copyPurchaseOrder(event.target.value)}><option value="">Standalone receipt</option>{orders.items.map(po => <option key={po.id} value={po.id}>{po.number} · {po.supplierName} · {po.status}</option>)}</Select></Field><p className="field-hint">Saving a linked delivery note locks PO editing, even while the receipt is a draft. A PO can have multiple receipts.</p>
      <div className="form-grid"><Field label="Supplier"><Select required disabled={!!draft.purchaseOrderId} value={draft.supplierId} onChange={event => setDraft(value => ({ ...value, supplierId: event.target.value }))}><option value="">Choose supplier</option>{suppliers.items.filter(item => item.isActive || item.id === draft.supplierId).map(item => <option key={item.id} value={item.id}>{item.code} · {item.name}{!item.isActive ? ' (inactive)' : ''}</option>)}</Select></Field><Field label="Received date & time (local)"><input type="datetime-local" required value={draft.receivedAt} onChange={event => setDraft(value => ({ ...value, receivedAt: event.target.value }))} /></Field><Field label="Supplier delivery reference (optional)"><input maxLength={100} value={draft.supplierReference} onChange={event => setDraft(value => ({ ...value, supplierReference: event.target.value }))} /></Field></div><Field label="General remarks"><textarea maxLength={2000} value={draft.remarks} onChange={event => setDraft(value => ({ ...value, remarks: event.target.value }))} /></Field></section>
      <div className="flex flex-wrap items-center gap-3"><Button variant="secondary" icon="plus" disabled={draft.lines.length >= 500} onClick={() => setDraft(value => ({ ...value, lines: [...value.lines, { productId: '', quantity: 0, lots: [emptyLot()] }] }))}>Add product</Button><Button variant="ghost" icon="refresh" onClick={() => { products.refresh(); suppliers.refresh(); placements.refresh(); orders.refresh() }}>Refresh choices</Button>{user?.permissions.includes('products.manage') && <Link className="text-button" to="/products" target="_blank" rel="noreferrer">Create a product in a new tab</Link>}</div>
      {!draft.lines.length && <div className="panel"><EmptyState title="No received items" detail="Add actual products, or save this draft and complete it later." /></div>}
      {draft.lines.map((line, lineIndex) => {
        const product = products.items.find(item => item.id === line.productId); const lotTotal = line.lots.reduce((sum, lot) => sum + (Number.isFinite(lot.quantity) ? lot.quantity : 0), 0)
        return <section className="panel receipt-line" key={lineIndex}><div className="section-header"><div><h2>Received product {lineIndex + 1}{line.supplierCode ? ` · ${line.supplierCode} · ${line.supplierName}` : ''}</h2><p>{product?.trackExpiry ? 'Expiry required on positive lots before posting' : line.supplierProductId && !line.productId ? 'Matched by code, or created automatically when this draft is saved. No external product link is needed.' : 'Batch and expiry are independently optional'}</p></div><Button variant="danger" onClick={() => setDraft(value => ({ ...value, lines: value.lines.filter((_, index) => index !== lineIndex) }))}>Remove product</Button></div><div className="p-3 space-y-3"><div className="form-grid"><Field label="Actual product"><Select required={!line.supplierProductId} value={line.productId} onChange={event => lineChange(lineIndex, { productId: event.target.value })}><option value="">{line.supplierProductId ? `Automatic by code: ${line.supplierCode}` : 'Choose received product'}</option>{products.items.filter(item => item.isActive || item.id === line.productId).map(item => <option key={item.id} value={item.id}>{item.code} · {item.name}{!item.isActive ? ' (inactive)' : ''}</option>)}</Select></Field><Field label="Actually received units"><input type="number" inputMode="numeric" min={0} step={1} max={Number.MAX_SAFE_INTEGER} required value={Number.isFinite(line.quantity) ? line.quantity : ''} onChange={event => { const quantity = event.target.valueAsNumber; lineChange(lineIndex, { quantity, ...(line.lots.length === 1 ? { lots: [{ ...line.lots[0], quantity }] } : {}) }) }} /></Field></div>
          <p className="field-hint">Lots total: {lotTotal.toLocaleString()} / {formatUnits(line.quantity)} units. Different lots remain independently traceable even when supplier batch codes match.</p>
          {line.lots.map((lot, lotIndex) => <details className="lot-editor" key={lotIndex} open><summary>Lot {lotIndex + 1} · {formatUnits(lot.quantity)} units</summary><div className="space-y-3 p-3"><div className="form-grid"><Field label="Lot units"><input type="number" inputMode="numeric" min={0} step={1} max={Number.MAX_SAFE_INTEGER} required value={Number.isFinite(lot.quantity) ? lot.quantity : ''} onChange={event => lotChange(lineIndex, lotIndex, { quantity: event.target.valueAsNumber })} /></Field><Field label="Supplier batch (optional)"><input maxLength={100} value={lot.supplierBatch} onChange={event => lotChange(lineIndex, lotIndex, { supplierBatch: event.target.value })} /></Field><Field label={product?.trackExpiry ? 'Expiry (required before posting)' : 'Expiry (optional)'}><input type="date" value={lot.expiry} onChange={event => lotChange(lineIndex, lotIndex, { expiry: event.target.value })} /></Field></div><AllocationEditor allocations={lot.allocations} choices={placements.items} expected={lot.quantity} onChange={allocations => lotChange(lineIndex, lotIndex, { allocations })} /><Button variant="danger" onClick={() => lineChange(lineIndex, { lots: line.lots.filter((_, index) => index !== lotIndex) })}>Remove lot</Button></div></details>)}
          <Button variant="secondary" icon="plus" disabled={line.lots.length >= 100 || !Number.isFinite(line.quantity)} onClick={() => lineChange(lineIndex, { lots: [...line.lots, emptyLot(Math.max(0, line.quantity - lotTotal))] })}>Add separate lot</Button>
        </div></section>
      })}
    </fieldset><div className="editor-actions"><Link className="secondary" to={id ? `/delivery-notes/${id}` : '/delivery-notes'}>Cancel</Link><Button type="submit" loading={busy}>Save draft</Button></div></form>}{confirmation}
  </>
}
