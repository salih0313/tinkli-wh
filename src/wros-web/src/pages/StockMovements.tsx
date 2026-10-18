import { useState } from 'react'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { useResource } from '../hooks/useResource'
import type { Page } from '../api/types'
import type { LedgerRow } from '../features/inventory/types'
import { LedgerTable } from '../features/inventory/LedgerTable'
import { ErrorMessage, Field, Heading } from '../components/Form'
import { Input, Select } from '../components/ui/Controls'
import { Pagination } from '../components/data-display/Pagination'
import { LoadingSkeleton, EmptyState } from '../components/feedback/States'
export default function StockMovements() {
  const [page, setPage] = useState(1),
    [search, setSearch] = useState(''),
    [type, setType] = useState('')
  const settledSearch = useDebouncedValue(search)
  const r = useResource<Page<LedgerRow>>(
    `/api/v1/inventory/movements?${new URLSearchParams({ page: String(page), pageSize: '20', search: settledSearch, type })}`,
  )
  return (
    <>
      <Heading
        title="Stock movements"
        detail="Immutable history. Negative quantities leave a location; positive quantities enter it. Paired transfer entries preserve company stock."
      />
      <section className="panel grid gap-4 sm:grid-cols-2">
        <Field label="Product, receipt or correlation ID">
          <Input
            value={search}
            onChange={(e) => {
              setSearch(e.target.value)
              setPage(1)
            }}
          />
        </Field>
        <Field label="Movement type">
          <Select
            value={type}
            onChange={(e) => {
              setType(e.target.value)
              setPage(1)
            }}
          >
            <option value="">All types</option>
            {[
              'Receipt',
              'RelocationIn',
              'RelocationOut',
              'SupplierReturn',
              'Disposal',
              'TransferIn',
              'TransferOut',
              'ExternalOrder',
            ].map((v) => (
              <option key={v}>{v}</option>
            ))}
          </Select>
        </Field>
      </section>
      <ErrorMessage error={r.error} />
      <section className="panel">
        {r.loading ? (
          <LoadingSkeleton />
        ) : r.data?.items.length ? (
          <LedgerTable items={r.data.items} />
        ) : (
          <EmptyState
            title="No matching movements"
            detail="Posted physical transactions will appear here."
          />
        )}
      </section>
      <Pagination
        page={page}
        total={r.data?.total ?? 0}
        noun="movements"
        onPage={setPage}
      />
    </>
  )
}
