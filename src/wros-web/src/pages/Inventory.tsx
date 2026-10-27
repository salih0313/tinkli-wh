import { useSearchParams } from 'react-router-dom'
import { InventoryNav } from '../features/inventory/InventoryNav'
import { Icon } from '../components/ui/Icon'
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
  const [routeParams] = useSearchParams()
  const [revision, setRevision] = useState(0)
  const [page, setPage] = useState(1),
    [search, setSearch] = useState(''),
    [expiry, setExpiry] = useState(expiryOnly ? 'near' : 'all'),
    [sort, setSort] = useState('sku'),
    [filters, setFilters] = useState<Record<string, string>>({ warehouseId: routeParams.get('warehouseId') ?? '' }),
    [summary, setSummary] = useState(!expiryOnly)
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
        title={expiryOnly ? 'Expiry alerts' : 'Products & stock'}
        detail={expiryOnly ? 'Review batches and locations that need attention before stock expires.' : 'One product, one total. Expand a row to see stock across receipts and locations.'}
        action={
          <Button
            icon="refresh"
            variant="secondary"
            onClick={() => {
              setRevision(value => value + 1)
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
      <InventoryNav />
      <section className="panel inventory-filters">
        <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Field label="Find a product" width="wide">
            <Input
              placeholder="Search SKU, name, barcode or brand…"
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
              {!summary && <option value="fifo">FIFO · oldest receipt</option>}
              {!summary && <option value="fefo">FEFO · earliest expiry</option>}
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
        <details className="inventory-advanced">
          <summary>Filter by warehouse, area or rack</summary>
          <div className="grid gap-3 sm:grid-cols-3">
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
          </div>
        </details>
        <div className="inventory-filter-footer">
          <span>{filters.warehouseId ? `Warehouse: ${warehouses.items.find(w => w.id === filters.warehouseId)?.name ?? 'Selected warehouse'} · ` : ''}Totals reflect your selected filters.</span>
          <Button variant="ghost" onClick={() => { setSearch(''); setExpiry(expiryOnly ? 'near' : 'all'); setSort('sku'); setFilters({}); setPage(1) }}>Clear filters</Button>
        </div>
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
      <section className="panel inventory-results" aria-busy={summary ? products.loading : loading}>
        <div className="inventory-result-toolbar">
          <h2>{expiryOnly ? 'Stock to review' : 'Current inventory'} <span>({((summary ? products.data : data)?.total ?? 0).toLocaleString()} {summary ? 'products' : 'placements'})</span></h2>
          <div className="inventory-view-switch" role="group" aria-label="Inventory view">
            <Button variant="ghost" aria-pressed={summary} onClick={() => { setSummary(true); setSort('sku'); setPage(1) }}>Products</Button>
            <Button variant="ghost" aria-pressed={!summary} onClick={() => { setSummary(false); setPage(1) }}>Locations</Button>
          </div>
        </div>
        {(summary ? products.loading : loading) ? (
          <LoadingSkeleton />
        ) : (summary ? products.error : error) ? (<EmptyState title="Inventory unavailable" detail="Use Refresh to try again." />) : summary && products.data?.items.length ? (
          <>
            <div className="inventory-product-head" aria-hidden="true"><span>Product / SKU</span><span>On hand</span><span>Sellable</span><span>Unavailable</span><span /></div>
            {products.data?.items.map((p) => (
              <ProductGroup
                key={p.productId}
                product={p}
                query={params.toString()}
                revision={revision}
              />
            ))}
          </>
        ) : !summary && data?.items.length ? (
          <StockTable items={data.items} />
        ) : (
          <EmptyState
            title="No matching stock"
            detail="Change the filters or post a delivery note to add physical inventory."
          />
        )}
      </section>
      <p className="inventory-footnote">On hand includes all physical stock. Unavailable units are expired or belong to inactive products. Sellable stock excludes these units.</p>
      <Pagination
        page={page}
        total={(summary ? products.data : data)?.total ?? 0}
        noun={summary ? 'products' : 'placements'}
        onPage={setPage}
        disabled={summary ? products.loading : loading}
      />
    </>
  )
}
function ProductGroup({
  product: p,
  query,
  revision,
}: {
  product: ProductSummary
  query: string
  revision: number
}) {
  const [open, setOpen] = useState(false)
  return (
    <details
      className="inventory-product"
      onToggle={(e) => setOpen(e.currentTarget.open)}
    >
      <summary className="inventory-product-row">
        <span className="inventory-product-name"><strong>{p.product}</strong><small>{p.sku}</small></span>
        <span className="inventory-number"><small>On hand</small><strong>{p.physical.toLocaleString()}</strong></span>
        <span className="inventory-number sellable"><small>Sellable</small><strong>{p.sellable.toLocaleString()}</strong></span>
        <span className="inventory-number"><small>Unavailable</small><strong>{(p.physical - p.sellable).toLocaleString()}</strong></span>
        <span className="inventory-expand"><Icon name="chevron" /><span className="sr-only">View stock locations for {p.product}</span></span>
      </summary>
      {open && <div className="inventory-product-detail"><p className="inventory-detail-hint">Remaining stock by receipt and location · quantities included in the product total</p><ProductLots key={revision} productId={p.productId} query={query} /></div>}
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
      ) : r.error ? <EmptyState title="Locations unavailable" detail="Refresh inventory to try again." /> : !r.data?.items.length ? <EmptyState title="No remaining stock" detail="This product has no stock matching the selected filters." /> : (
        <StockTable items={r.data.items} />
      )}
      <Pagination
        page={page}
        total={r.data?.total ?? 0}
        noun="placements"
        onPage={setPage}
        disabled={r.loading}
      />
    </>
  )
}
