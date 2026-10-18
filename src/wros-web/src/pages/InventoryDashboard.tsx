import { Link } from 'react-router-dom'
import { useResource } from '../hooks/useResource'
import { ErrorMessage, Heading } from '../components/Form'
import { LoadingSkeleton } from '../components/feedback/States'
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
  return (
    <>
      <Heading
        title="Inventory overview"
        detail="Live physical inventory across all warehouses · whole units"
      />
      <ErrorMessage error={r.error} />
      {r.loading ? (
        <LoadingSkeleton />
      ) : (
        r.data && (
          <>
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {[
                ['Products / SKUs', `${r.data.products} / ${r.data.skus}`],
                ['Physical units', r.data.physical.toLocaleString()],
                ['Sellable units', r.data.sellable.toLocaleString()],
                ['Expired units', r.data.expired.toLocaleString()],
              ].map(([label, value]) => (
                <section className="panel" key={label}>
                  <p className="field-hint">{label}</p>
                  <strong className="text-3xl">{value}</strong>
                </section>
              ))}
            </div>
            <section className="panel">
              <h2 className="text-xl font-semibold">
                Attention and availability
              </h2>
              <p className="my-3">
                {r.data.approachingExpiry} lots expire within{' '}
                {r.data.expiryAlertDays} days. Low stock: {r.data.lowStock}.
              </p>
              <div className="flex flex-wrap gap-4">
                <Link className="button secondary" to="/inventory">
                  Explore stock
                </Link>
                <Link className="button secondary" to="/expiry">
                  Review expiry
                </Link>
              </div>
            </section>
            <section className="panel">
              <h2 className="mb-4 text-xl font-semibold">Warehouses</h2>
              <div className="grid gap-4 sm:grid-cols-2">
                {r.data.warehouses.map((w) => (
                  <div key={w.warehouseId} className="rounded-xl border p-4">
                    <strong>{w.name}</strong>
                    <p>
                      {w.physical.toLocaleString()} physical /{' '}
                      {w.sellable.toLocaleString()} sellable units
                    </p>
                  </div>
                ))}
              </div>
              {!r.data.warehouses.length && <p>No posted stock yet.</p>}
            </section>
            <section className="panel">
              <h2 className="mb-4 text-xl font-semibold">Recent movements</h2>
              <LedgerTable items={r.data.recentMovements.items} />
            </section>
          </>
        )
      )}
    </>
  )
}
