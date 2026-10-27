import { InventoryNav } from '../features/inventory/InventoryNav'
import { Button } from '../components/ui/Controls'
import { Icon } from '../components/ui/Icon'
import { Link } from 'react-router-dom'
import { useResource } from '../hooks/useResource'
import { ErrorMessage, Heading } from '../components/Form'
import { EmptyState, LoadingSkeleton } from '../components/feedback/States'
import { LedgerTable } from '../features/inventory/LedgerTable'
import type { LedgerRow } from '../features/inventory/types'
import type { Page } from '../api/types'
type Dashboard = {
  products: number
  skus: number
  physical: number
  sellable: number
  expired: number
  approachingExpiry: number
  expiryAlertDays: number
  lowStock: string
  warehouses: {
    warehouseId: string
    name: string
    physical: number
    sellable: number
  }[]
  recentMovements: Page<LedgerRow>
}
export default function InventoryDashboard() {
  const r = useResource<Dashboard>('/api/v1/inventory/dashboard')
  const d = r.data
  return <>
    <Heading title="Inventory overview" detail="Stock on hand, warehouse availability and items that need attention."
      action={<Button icon="refresh" variant="secondary" disabled={r.loading} onClick={r.refresh}>Refresh</Button>} />
    <InventoryNav />
    <ErrorMessage error={r.error} />
    {r.loading ? <LoadingSkeleton /> : !d ? <section className="panel"><EmptyState title="Overview unavailable" detail="Use Refresh to try again." /></section> : <>
      <div className="inventory-metrics">
        {[
          { label: 'On hand', value: d.physical, hint: 'Physical units across all warehouses', icon: 'warehouse' as const, tone: '' },
          { label: 'Sellable', value: d.sellable, hint: 'Active products within valid expiry', icon: 'check' as const, tone: 'sellable' },
          { label: 'Expired', value: d.expired, hint: 'On hand, excluded from sellable stock', icon: 'alert' as const, tone: 'expired' },
          { label: 'Catalog products', value: d.products, hint: 'Includes products with no stock', icon: 'overview' as const, tone: '' },
        ].map(m => <section className={`panel inventory-metric ${m.tone}`} key={m.label}><div><span>{m.label}</span><Icon name={m.icon} /></div><strong>{m.value.toLocaleString()}</strong><p>{m.hint}</p></section>)}
      </div>
      <section className="panel inventory-attention">
        <span className="inventory-attention-icon"><Icon name="alert" /></span>
        <div><h2>{d.approachingExpiry.toLocaleString()} lots approaching expiry</h2><p>Within {d.expiryAlertDays} days · {d.expired.toLocaleString()} expired units on hand. Review batches and rack locations.</p></div>
        <Link className="button secondary" to="/expiry">Review expiry <Icon name="arrow" /></Link>
      </section>
      <section className="panel inventory-warehouse-section">
        <div className="inventory-section-heading"><div><h2>Stock by warehouse</h2><p>Current remaining quantities from all posted receipts.</p></div><Link className="text-button" to="/inventory">View all stock →</Link></div>
        {d.warehouses.length ? <div className="inventory-warehouse-grid">{d.warehouses.map(w => <Link className="inventory-warehouse-card" key={w.warehouseId} to={`/inventory?warehouseId=${w.warehouseId}`}><div><Icon name="warehouse" /><h3>{w.name}</h3><Icon name="arrow" /></div><dl><div><dt>On hand</dt><dd>{w.physical.toLocaleString()}</dd></div><div><dt>Sellable</dt><dd className="sellable">{w.sellable.toLocaleString()}</dd></div></dl><span>View products & locations</span></Link>)}</div> : <EmptyState title="No stock received yet" detail="Post a fully allocated delivery note to see warehouse quantities here." />}
      </section>
      <section className="panel inventory-results">
        <div className="inventory-section-heading"><div><h2>Recent movements</h2><p>Latest receipts, deductions and location changes.</p></div><Link className="text-button" to="/stock-movements">All movements →</Link></div>
        {d.recentMovements.items.length ? <LedgerTable items={d.recentMovements.items} /> : <EmptyState title="No movements yet" detail="Posted stock transactions will appear here." />}
      </section>
      <p className="inventory-footnote">On hand includes expired and inactive stock. Sellable excludes it. All quantities are whole units.</p>
    </>}
  </>
}
