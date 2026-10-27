import { notifySuccess } from '../components/feedback/notifications'
import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { get, mutate } from '../api/client'
import type { Allocation, Placement, Receipt } from '../features/receiving/types'
import { AllocationEditor } from '../features/receiving/AllocationEditor'
import { useLookup } from '../hooks/useLookup'
import { useConfirm } from '../hooks/useConfirm'
import { ErrorMessage, Heading } from '../components/Form'
import { Button } from '../components/ui/Controls'
import { LoadingSkeleton } from '../components/feedback/States'
export default function ReceiptCorrection() {
  const { id } = useParams(); const navigate = useNavigate(); const { confirm, confirmation } = useConfirm()
  const choices = useLookup<Placement>('/api/v1/delivery-notes/placements')
  const [receipt, setReceipt] = useState<Receipt | null>(null); const [allocations, setAllocations] = useState<Record<string, Allocation[]>>({})
  const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false); const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    const controller = new AbortController()
    get<Receipt>(`/api/v1/delivery-notes/${id}`, controller.signal).then(value => { setReceipt(value); setAllocations(Object.fromEntries(value.lines.flatMap(line => line.lots).filter(lot => lot.quantity > 0).map(lot => [lot.id, lot.placements]))) }).catch(failure => { if (!controller.signal.aborted) setError(failure) }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [id])
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!receipt) return
    const lots = receipt.lines.flatMap(line => line.lots).filter(lot => lot.quantity > 0)
    if (lots.some(lot => (allocations[lot.id] ?? []).some(a => !a.rackId || !Number.isSafeInteger(a.quantity) || a.quantity < 1) || (allocations[lot.id] ?? []).reduce((sum, a) => sum + a.quantity, 0) !== lot.quantity)) { setError('Preserve every lot’s original received total using positive whole-unit allocations.'); return }
    if (!lots.length) { setError('This receipt has no positive-quantity lots to relocate.'); return }
    if (!await confirm('Record this physical rack correction? Total received quantities stay fixed. The system will append relocation movements and reject unavailable or consumed source stock.')) return
    setBusy(true); setError(null)
    try { await mutate('POST', `/api/v1/delivery-notes/${id}/correct-allocations`, { rowVersion: receipt.rowVersion, lots: lots.map(lot => ({ stockLotId: lot.id, allocations: allocations[lot.id].map(({ rackId, locationId, quantity }) => ({ rackId, locationId, quantity })) })) }); notifySuccess('Allocation correction recorded.'); navigate(`/delivery-notes/${id}`, { replace: true }) }
    catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  return <><Heading eyebrow="Receiving" title="Correct rack allocations" detail="Record rack corrections within each warehouse, preserving receipt quantities, lot identity and original history." /><ErrorMessage error={error || choices.error} />
    {loading || choices.loading ? <div className="panel"><LoadingSkeleton label="Loading current allocations…" /></div> : receipt?.status === 'Posted' ? <form onSubmit={save} className="space-y-3" aria-busy={busy}><p className="notice border-brand-100 bg-brand-50 text-brand-700">Edit the nominal placement of the full received quantity. Only the physically remaining portion can move; any consumed portion must stay assigned to its existing source. Original receipt allocations and each warehouse’s total remain unchanged. Warehouse-to-warehouse transfers are deferred.</p>{receipt.lines.map(line => line.lots.filter(lot => lot.quantity > 0).map((lot, lotIndex) => <section className="panel p-3 space-y-3" key={lot.id}><h2 className="text-sm font-semibold">{line.sku} · {line.name}</h2><p className="field-hint break-all">Lot {lotIndex + 1} · Batch {lot.supplierBatch ?? 'not provided'} · {lot.quantity.toLocaleString()} originally received units</p><details className="revision-entry"><summary>Available source stock</summary><ul className="space-y-2 mt-3 text-sm">{lot.balances.map(balance => <li key={balance.id}>{balance.path}: <strong>{balance.quantity.toLocaleString()}</strong> physically available units</li>)}</ul></details><AllocationEditor allocations={allocations[lot.id] ?? []} choices={choices.items} expected={lot.quantity} disabled={busy} onChange={items => setAllocations(value => ({ ...value, [lot.id]: items }))} /></section>))}<div className="editor-actions"><Link className="secondary" to={`/delivery-notes/${id}`}>Cancel</Link><Button type="submit" loading={busy}>Record allocation correction</Button></div></form> : <ErrorMessage error="Open a posted delivery note to correct allocations." />}{confirmation}
  </>
}
