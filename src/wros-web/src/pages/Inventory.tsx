import { useEffect, useState } from 'react'
import { useLookup } from '../hooks/useLookup'
import type { MasterRecord } from '../features/master-data/types'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { useResource } from '../hooks/useResource'
import { ErrorMessage, Field, Heading } from '../components/Form'
import { Button, Input, Select } from '../components/ui/Controls'
import { Pagination } from '../components/data-display/Pagination'
import { EmptyState, LoadingSkeleton } from '../components/feedback/States'
import type { Page } from '../api/types'
import type { InventoryRow, ProductSummary } from '../features/inventory/types'
import { StockTable } from '../features/inventory/StockTable'
export default function Inventory({
  expiryOnly = false,
}: {
  expiryOnly?: boolean
}) {
  const [page, setPage] = useState(1),
    [search, setSearch] = useState(''),
    [expiry, setExpiry] = useState(expiryOnly ? 'near' : 'all'),
    [sort, setSort] = useState('sku'),
    [filters, setFilters] = useState<Record<string, string>>({}),
    [summary, setSummary] = useState(false)
  const warehouses = useLookup<MasterRecord>('/api/v1/warehouses'),
    areas = useLookup<MasterRecord>('/api/v1/areas'),
    racks = useLookup<MasterRecord>('/api/v1/racks')
  const settledSearch = useDebouncedValue(search)
  const params = new URLSearchParams({
    page: String(page),
    pageSize: '20',
    search: settledSearch,
    expiry,
    sort,
    ...filters,
  })
  const { data, loading, error, refresh } = useResource<Page<InventoryRow>>(
    summary ? null : `/api/v1/inventory?${params}`,
  )
  const products = useResource<Page<ProductSummary>>(
    summary ? `/api/v1/inventory/products?${params}` : null,
  )
  function change(key: string, value: string) {
    setPage(1)
    setFilters((v) => ({ ...v, [key]: value }))
  }
  return (
    <>
      <Heading
        title={expiryOnly ? 'Expiry alerts' : 'Inventory explorer'}
        detail="Physical units remain accounted for. Expired and inactive products are excluded from sellable units. FIFO and FEFO are suggestions only."
        action={
          <Button
            icon="refresh"
            variant="secondary"
            onClick={() => {
              refresh()
              products.refresh()
              warehouses.refresh()
              areas.refresh()
              racks.refresh()
            }}
          >
            Refresh
          </Button>
        }
      />
      <section className="panel p-3">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="SKU, name, barcode or brand">
            <Input
              value={search}
              onChange={(e) => {
                setSearch(e.target.value)
                setPage(1)
              }}
            />
          </Field>
          <Field label="Expiry">
            <Select
              value={expiry}
              onChange={(e) => {
                setExpiry(e.target.value)
                setPage(1)
              }}
            >
              <option value="all">All physical stock</option>
              <option value="near">Approaching expiry</option>
              <option value="expired">Expired</option>
              <option value="sellable">Sellable only</option>
            </Select>
          </Field>
          <Field label="Order">
            <Select
              value={sort}
              onChange={(e) => {
                setSort(e.target.value)
                if (['fifo', 'fefo'].includes(e.target.value))
                  setExpiry('sellable')
                setPage(1)
              }}
            >
              <option value="sku">SKU</option>
              <option value="fifo">FIFO · oldest receipt</option>
              <option value="fefo">FEFO · earliest expiry</option>
              <option value="quantity">Largest quantity</option>
            </Select>
          </Field>
          <Field label="Supplier batch">
            <Input
              value={filters.batch ?? ''}
              onChange={(e) => change('batch', e.target.value)}
            />
          </Field>
        </div>
        <details className="mt-4">
          <summary className="cursor-pointer py-3">
            Filter by warehouse, area, rack or lot
          </summary>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label="Warehouse">
              <Select
                value={filters.warehouseId ?? ''}
                onChange={(e) => {
                  setPage(1)
                  setFilters((v) => ({
                    ...v,
                    warehouseId: e.target.value,
                    areaId: '',
                    rackId: '',
                  }))
                }}
              >
                <option value="">All warehouses</option>
                {warehouses.items.map((w) => (
                  <option key={w.id} value={w.id}>
                    {w.code} · {w.name}
                  </option>
                ))}
              </Select>
            </Field>
            <Field label="Area">
              <Select
                value={filters.areaId ?? ''}
                onChange={(e) => {
                  setPage(1)
                  setFilters((v) => ({
                    ...v,
                    areaId: e.target.value,
                    rackId: '',
                  }))
                }}
              >
                <option value="">All areas</option>
                {areas.items
                  .filter(
                    (a) =>
                      !filters.warehouseId ||
                      a.parentId === filters.warehouseId,
                  )
                  .map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.parentPath} / {a.code}
                    </option>
                  ))}
              </Select>
            </Field>
            <Field label="Rack">
              <Select
                value={filters.rackId ?? ''}
                onChange={(e) => change('rackId', e.target.value)}
              >
                <option value="">All racks</option>
                {racks.items
                  .filter(
                    (r) =>
                      (!filters.areaId || r.parentId === filters.areaId) &&
                      (!filters.warehouseId ||
                        areas.items.some(
                          (a) =>
                            a.id === r.parentId &&
                            a.parentId === filters.warehouseId,
                        )),
                  )
                  .map((r) => (
                    <option key={r.id} value={r.id}>
                      {r.parentPath} / {r.code}
                    </option>
                  ))}
              </Select>
            </Field>
            <Field label="Lot ID">
              <Input
                placeholder="UUID (optional)"
                value={filters.lotId ?? ''}
                onChange={(e) => change('lotId', e.target.value)}
              />
            </Field>
          </div>
        </details>
        <label className="mt-4 flex min-h-11 items-center gap-3">
          <input
            type="checkbox"
            checked={summary}
            onChange={(e) => {
              setSummary(e.target.checked)
              setPage(1)
            }}
          />
          Group by product; expand for lot locations
        </label>
      </section>
      <ErrorMessage
        error={
          error ||
          products.error ||
          warehouses.error ||
          areas.error ||
          racks.error
        }
      />
      <section className="panel">
        {(summary ? products.loading : loading) ? (
          <LoadingSkeleton />
        ) : summary ? (
          <>
            {products.data?.items.map((p) => (
              <ProductGroup
                key={p.productId}
                product={p}
                query={params.toString()}
              />
            ))}
          </>
        ) : data?.items.length ? (
          <StockTable items={data.items} />
        ) : (
          <EmptyState
            title="No matching stock"
            detail="Change the filters or post a delivery note to add physical inventory."
          />
        )}
      </section>
      <Pagination
        page={page}
        total={(summary ? products.data : data)?.total ?? 0}
        noun="results"
        onPage={setPage}
        disabled={summary ? products.loading : loading}
      />
    </>
  )
}
function ProductGroup({
  product: p,
  query,
}: {
  product: ProductSummary
  query: string
}) {
  const [open, setOpen] = useState(false)
  return (
    <details
      className="border-b py-4"
      onToggle={(e) => setOpen(e.currentTarget.open)}
    >
      <summary className="cursor-pointer py-3">
        <strong>
          {p.sku} · {p.product}
        </strong>
        <span className="block text-sm">
          {p.physical} physical / {p.sellable} sellable units
        </span>
      </summary>
      {open && <ProductLots productId={p.productId} query={query} />}
    </details>
  )
}
function ProductLots({
  productId,
  query,
}: {
  productId: string
  query: string
}) {
  const [page, setPage] = useState(1)
  useEffect(() => setPage(1), [query])
  const detailsQuery = new URLSearchParams(query)
  detailsQuery.set('productId', productId)
  detailsQuery.set('page', String(page))
  detailsQuery.set('pageSize', '20')
  const r = useResource<Page<InventoryRow>>(`/api/v1/inventory?${detailsQuery}`)
  return (
    <>
      <ErrorMessage error={r.error} />
      {r.loading ? (
        <LoadingSkeleton />
      ) : (
        <StockTable items={r.data?.items ?? []} />
      )}
      <Pagination
        page={page}
        total={r.data?.total ?? 0}
        noun="placements"
        onPage={setPage}
      />
    </>
  )
}
