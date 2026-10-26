import { notifySuccess } from '../components/feedback/notifications'
import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useBlocker, useNavigate, useParams } from 'react-router-dom'
import { get, mutate } from '../api/client'
import type { PurchaseOrder, OrderLine } from '../features/purchasing/types'
import type { MasterRecord } from '../features/master-data/types'
import type { CatalogProduct } from '../features/purchasing/catalog'
import { discountLabel, fils, formatFils, priceLabel, productSnapshot } from '../features/purchasing/catalog'
import { ExcelImportDialog } from '../features/purchasing/ExcelImportDialog'
import { CatalogPicker, SupplierProductDialog } from '../features/purchasing/CatalogDialogs'
import { PricingSummary } from '../features/purchasing/PricingSummary'
import { useLookup, allPages } from '../hooks/useLookup'
import { ErrorMessage, Field, Heading } from '../components/Form'
import { Button, Select } from '../components/ui/Controls'
import { Dialog } from '../components/ui/Dialog'
import { LoadingSkeleton } from '../components/feedback/States'
import { useConfirm } from '../hooks/useConfirm'

export default function PurchaseOrderEditor() {
  const { id } = useParams(); const navigate = useNavigate(); const { confirm, confirmation } = useConfirm()
  const suppliers = useLookup<MasterRecord>('/api/v1/suppliers')
  const [order, setOrder] = useState<PurchaseOrder | null>(null); const [lines, setLines] = useState<OrderLine[]>([])
  const [supplierId, setSupplierId] = useState(''); const [dirty, setDirty] = useState(false); const saved = useRef(false)
  const [loading, setLoading] = useState(!!id); const [busy, setBusy] = useState(false); const [error, setError] = useState<unknown>(null)
  const [dialog, setDialog] = useState<'import' | 'catalog' | 'manual' | null>(null); const [editing, setEditing] = useState<CatalogProduct | null>(null)
  const [catalog, setCatalog] = useState<Record<string, CatalogProduct>>({}); const [currentImport, setCurrentImport] = useState<string[]>([])
  const [scope, setScope] = useState<'all' | 'current'>('all'); const [search, setSearch] = useState(''); const [common, setCommon] = useState(1)
  const blocker = useBlocker(() => dirty && !saved.current)
  useEffect(() => { const warn = (e: BeforeUnloadEvent) => { if (dirty && !saved.current) { e.preventDefault(); e.returnValue = '' } }; window.addEventListener('beforeunload', warn); return () => window.removeEventListener('beforeunload', warn) }, [dirty])
  useEffect(() => {
    if (!id) return
    const controller = new AbortController()
    get<PurchaseOrder>(`/api/v1/purchase-orders/${id}`, controller.signal).then(value => { setOrder(value); setSupplierId(value.supplierId); setLines(value.lines.map(l => ({ ...l, originalLineId: l.id }))) }).catch(failure => { if (!controller.signal.aborted) setError(failure) }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [id])
  function add(items: CatalogProduct[], fromImport = false) {
    setCatalog(current => ({ ...current, ...Object.fromEntries(items.map(p => [p.id, p])) }))
    setLines(current => [...current.map(line => {
      const item = fromImport ? items.find(p => p.id === line.supplierProductId) : undefined
      return item ? { ...line, productId: item.productId, sku: item.code, name: item.name, pricing: productSnapshot(item, line.quantity), catalogRowVersion: item.rowVersion, refreshPrice: true } : line
    }), ...items.filter(p => !current.some(l => l.supplierProductId === p.id)).map(p => ({ productId: p.productId, supplierProductId: p.id, quantity: 1, sku: p.code, name: p.name, pricing: productSnapshot(p, 1), catalogRowVersion: p.rowVersion }))])
    if (fromImport) { setCurrentImport(items.map(p => p.id)); setScope('current') } else setScope('all')
    setDirty(true); setDialog(null)
  }
  async function editProduct(line: OrderLine) {
    if (!line.supplierProductId) return
    setError(null)
    try { const p = (await allPages<CatalogProduct>(`/api/v1/suppliers/${supplierId}/catalog?search=${encodeURIComponent(line.sku ?? '')}`)).find(p => p.id === line.supplierProductId); if (!p) throw new Error('Supplier catalog record not found.'); setEditing(p); setDialog('manual') } catch (e) { setError(e) }
  }
  async function refreshPrices() {
    if (!await confirm('Refresh all catalog prices from the latest supplier catalog? This replaces saved prices on this editable draft.')) return
    setBusy(true); setError(null)
    try {
      const latest = await allPages<CatalogProduct>(`/api/v1/suppliers/${supplierId}/catalog`)
      const byId = Object.fromEntries(latest.map(p => [p.id, p]));
      if (lines.some(l => l.supplierProductId && (!byId[l.supplierProductId]?.isActive))) throw new Error('A supplier product is inactive. Review it before refreshing prices.')
      setCatalog(byId); setLines(current => current.map(l => { const p = l.supplierProductId ? byId[l.supplierProductId] : null; return p ? { ...l, pricing: productSnapshot(p, l.quantity), refreshPrice: true, catalogRowVersion: p.rowVersion } : l })); setDirty(true); notifySuccess('Catalog prices refreshed.')
    } catch (e) { setError(e) } finally { setBusy(false) }
  }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const form = new FormData(event.currentTarget)
    if (!lines.length || lines.length > 500) { setError('Add between 1 and 500 products before saving.'); return }
    const invalidQuantity = lines.find(l => !Number.isSafeInteger(l.quantity) || l.quantity < 1)
    if (invalidQuantity) { setError(`Enter a positive whole-unit quantity for ${invalidQuantity.sku}.`); return }
    setBusy(true); setError(null)
    try {
      const result = await mutate<PurchaseOrder>(id ? 'PUT' : 'POST', `/api/v1/purchase-orders${id ? `/${id}` : ''}`, { supplierId, orderDate: form.get('orderDate'), expectedDeliveryDate: form.get('expectedDeliveryDate') || null, remarks: form.get('remarks'), lines: lines.map(({ productId, quantity, supplierProductId, originalLineId, refreshPrice, catalogRowVersion }) => ({ productId: supplierProductId ? null : productId?.trim() || null, quantity, supplierProductId, originalLineId, refreshPrice, catalogRowVersion })), rowVersion: order?.rowVersion })
      notifySuccess('Purchase order saved.'); saved.current = true; setDirty(false); navigate(`/purchase-orders/${result.id}`, { replace: true })
    } catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  const visibleError = error || suppliers.error
  const shown = lines.map((line, index) => ({ line, index })).filter(({ line }) => (scope === 'all' || currentImport.includes(line.supplierProductId ?? '')) && `${line.sku} ${line.name} ${line.pricing?.barcode ?? ''}`.toLowerCase().includes(search.toLowerCase()))
  return <><Heading eyebrow="Purchasing" title={id ? `Edit ${order?.number ?? 'purchase order'}` : 'Prepare purchase order'} detail="Import supplier availability, select products and enter required quantities. Prices are snapshots; stock changes only through receiving." />
    <ErrorMessage error={visibleError} />
    {loading || suppliers.loading ? <div className="panel"><LoadingSkeleton label="Loading order form…" /></div> : id && !order ? <Link className="text-button" to="/purchase-orders">Return to purchase orders</Link> : order?.status === 'Locked' ? <ErrorMessage error="Receiving has started. This purchase order is locked." /> : <form onSubmit={save} onChange={() => setDirty(true)} className="po-editor space-y-3" aria-busy={busy}><fieldset disabled={busy} className="space-y-3"><section className="panel po-editor-panel space-y-2"><div className="po-order-fields"><Field label="Supplier"><Select name="supplierId" required value={supplierId} disabled={lines.length > 0} onChange={e => { setSupplierId(e.target.value); setCurrentImport([]); setScope('all') }}><option value="">Choose supplier</option>{suppliers.items.filter(s => s.isActive || s.id === order?.supplierId).map(s => <option value={s.id} key={s.id}>{s.code} · {s.name}</option>)}</Select></Field><div className="po-date-fields"><Field label="PO date"><input name="orderDate" type="date" required defaultValue={order?.orderDate ?? new Date().toISOString().slice(0, 10)} /></Field><Field label="Expected delivery date"><input name="expectedDeliveryDate" type="date" defaultValue={order?.expectedDeliveryDate ?? ''} /></Field></div></div><Field label="Remarks"><textarea name="remarks" rows={1} maxLength={2000} defaultValue={order?.remarks ?? ''} /></Field></section>
    <section className="panel po-editor-panel space-y-2"><div className="po-toolbar"><Button variant="secondary" disabled={!supplierId} onClick={() => setDialog('import')}>Import Excel</Button><Button variant="secondary" disabled={!supplierId} onClick={() => setDialog('catalog')}>Add existing products</Button><Button variant="secondary" disabled={!supplierId} onClick={() => { setEditing(null); setDialog('manual') }}>Add manually</Button><Button variant="ghost" disabled={!supplierId || !lines.length} onClick={() => void refreshPrices()}>Refresh catalog prices</Button></div>
      <div className="po-toolbar"><Field label="Common quantity"><input className="po-common" type="number" min={1} max={Number.MAX_SAFE_INTEGER} step={1} value={Number.isFinite(common) ? common : ''} onChange={e => setCommon(e.target.valueAsNumber)} /></Field><Button variant="secondary" disabled={!lines.length || !Number.isSafeInteger(common) || common < 1} onClick={() => { setLines(current => current.map(l => ({ ...l, quantity: common }))); setDirty(true) }}>Apply to all {lines.length} rows</Button><Field label="Search working list"><input type="search" value={search} onChange={e => setSearch(e.target.value)} /></Field>{currentImport.length > 0 && <Field label="Show"><Select value={scope} onChange={e => setScope(e.target.value as 'all' | 'current')}><option value="current">Current import</option><option value="all">All PO products</option></Select></Field>}</div>
      <p className="field-hint">Showing {shown.length} of {lines.length} rows. Common quantity applies to all PO rows, including filtered rows. Supplier quantity is informational and never limits your order. Remove all rows to change supplier.</p>
      {lines.length > 500 && <ErrorMessage error="A PO supports at most 500 lines. Remove unwanted rows before saving." />}
      <div className="po-scroll"><table className="po-table po-working"><colgroup><col className="po-col-code" /><col className="po-col-name" /><col className="po-col-brand" /><col className="po-col-barcode" /><col className="po-col-money" /><col className="po-col-price" /><col className="po-col-discount" /><col className="po-col-price" /><col className="po-col-quantity" /><col className="po-col-price" /><col className="po-col-actions" /></colgroup><thead><tr><th>Code</th><th>Product name</th><th>Brand</th><th>Barcode</th><th className="num">RSP</th><th className="num">Supplier price</th><th>Discount</th><th className="num">Net unit price</th><th className="num">Required qty</th><th className="num">Line total</th><th>Actions</th></tr></thead><tbody>{shown.map(({ line: l, index }) => <tr key={l.supplierProductId ?? l.originalLineId ?? index}><td data-label="Code">{l.sku}</td><td data-label="Product">{l.name}{catalog[l.supplierProductId ?? '']?.availableQuantity != null && <small>Supplier qty: {catalog[l.supplierProductId!].availableQuantity}</small>}</td><td data-label="Brand">{l.pricing?.brand ?? '—'}</td><td data-label="Barcode">{l.pricing?.barcode ?? '—'}</td><td className="num" data-label="RSP">{priceLabel(l.pricing?.rsp)}</td><td className="num" data-label="Supplier price">{priceLabel(l.pricing?.supplierPrice)}</td><td data-label="Discount">{discountLabel(l.pricing?.discountType, l.pricing?.discountValue)}</td><td className="num" data-label="Net unit">{priceLabel(l.pricing?.netUnitPrice)}</td><td className="num" data-label="Required qty"><input aria-label={`Required quantity for ${l.sku}`} type="number" inputMode="numeric" min={1} max={Number.MAX_SAFE_INTEGER} step={1} required value={Number.isFinite(l.quantity) ? l.quantity : ''} onChange={e => { const quantity = e.target.valueAsNumber; setLines(current => current.map((row, i) => i === index ? { ...row, quantity } : row)); setDirty(true) }} /></td><td className="num" data-label="Line total">{l.pricing?.netUnitPrice != null && Number.isSafeInteger(l.quantity) && l.quantity > 0 ? formatFils(fils(l.pricing.netUnitPrice) * BigInt(l.quantity)) : '—'}</td><td data-label="Actions"><div className="po-row-actions"><Button variant="ghost" onClick={() => void editProduct(l)}>Edit</Button><Button variant="ghost" aria-label={`Remove ${l.sku}`} onClick={() => { setLines(current => current.filter((_, i) => i !== index)); setDirty(true) }}>Remove</Button></div></td></tr>)}</tbody></table>{!shown.length && <p className="p-3 field-hint">{lines.length ? 'No products match this filter.' : 'Select a supplier, then import stock or add catalog products.'}</p>}</div>
    </section><PricingSummary lines={lines} /></fieldset><div className="editor-actions"><Link className="secondary" to={id ? `/purchase-orders/${id}` : '/purchase-orders'}>Cancel</Link><Button type="submit" loading={busy}>Save purchase order</Button></div></form>}
    {dialog === 'import' && <ExcelImportDialog supplierId={supplierId} onClose={() => setDialog(null)} onImported={items => add(items, true)} />}
    {dialog === 'catalog' && <CatalogPicker supplierId={supplierId} onClose={() => setDialog(null)} onAdd={items => add(items)} onEdit={p => { setEditing(p); setDialog('manual') }} />}
    {dialog === 'manual' && <SupplierProductDialog supplierId={supplierId} item={editing} onClose={() => setDialog(null)} onSaved={p => { setCatalog(current => ({ ...current, [p.id]: p })); if (lines.some(l => l.supplierProductId === p.id)) { setLines(current => current.map(l => l.supplierProductId === p.id && !l.originalLineId ? { ...l, productId: p.productId, pricing: productSnapshot(p, l.quantity), catalogRowVersion: p.rowVersion, sku: p.code, name: p.name } : l)); setDirty(true); setDialog(null) } else if (p.isActive) add([p]); else { setDialog('catalog') } }} />}
    <Dialog open={blocker.state === 'blocked'} onClose={() => { if (blocker.state === 'blocked') blocker.reset() }} title="Leave unsaved purchase order?" description="Your quantities and preparation changes have not been saved."><div className="dialog-actions"><Button variant="secondary" onClick={() => { if (blocker.state === 'blocked') blocker.reset() }}>Keep editing</Button><Button variant="danger" onClick={() => { if (blocker.state === 'blocked') blocker.proceed() }}>Discard and leave</Button></div></Dialog>{confirmation}
  </>
}
