import type { OrderLine } from './types'
import type { PricingSummary as SavedSummary } from './catalog'
import { fils, formatFils, exactMoney } from './catalog'
export function PricingSummary({ lines, saved }: { lines?: OrderLine[]; saved?: SavedSummary }) {
  const valid = (lines ?? []).filter(l => Number.isSafeInteger(l.quantity) && l.quantity > 0)
  const gross = valid.reduce((sum, l) => sum + (l.pricing ? fils(l.pricing.supplierPrice) * BigInt(l.quantity) : 0n), 0n)
  const net = valid.reduce((sum, l) => sum + (l.pricing ? fils(l.pricing.netUnitPrice) * BigInt(l.quantity) : 0n), 0n)
  const missing = saved?.unpricedLines ?? lines?.filter(l => !l.pricing).length ?? 0
  return <section className="po-summary" aria-label="Purchase order summary" aria-live="polite"><div><span>Selected products</span><strong>{saved?.selectedProducts ?? lines?.length ?? 0}</strong></div><div><span>Required quantity</span><strong>{saved ? BigInt(saved.totalQuantityText).toLocaleString() : valid.reduce((sum, l) => sum + BigInt(l.quantity), 0n).toLocaleString()}</strong></div><div><span>Gross (KWD)</span><strong>{saved ? exactMoney(saved.grossAmountText) : formatFils(gross)}</strong></div><div><span>Discount (KWD)</span><strong>{saved ? exactMoney(saved.discountAmountText) : formatFils(gross - net)}</strong></div><div className="po-net"><span>Net (KWD){missing ? ' · priced lines only' : ''}</span><strong>{saved ? exactMoney(saved.netAmountText) : formatFils(net)}</strong></div>{missing > 0 && <p className="field-hint">{missing} historical line(s) have no saved pricing. Financial totals exclude those lines.</p>}</section>
}
