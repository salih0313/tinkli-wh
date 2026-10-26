import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { mutate } from '../api/client'
import type { Page } from '../api/types'
import type { InventoryRow } from '../features/inventory/types'
import type { Placement } from '../features/receiving/types'
import { useDebouncedValue } from '../hooks/useDebouncedValue'
import { useResource } from '../hooks/useResource'
import { useLookup } from '../hooks/useLookup'
import { useConfirm } from '../hooks/useConfirm'
import { ErrorMessage, Field, Heading } from '../components/Form'
import { Button, Input, Select } from '../components/ui/Controls'
import { DataTable } from '../components/data-display/DataTable'
import { Pagination } from '../components/data-display/Pagination'
import { LoadingSkeleton, EmptyState } from '../components/feedback/States'
import { useSession } from '../auth/SessionContext'
type Kind = 'SupplierReturn' | 'Disposal' | 'Transfer'
type Transaction = {
  id: string
  number: string
  kind: Kind
  completedAt: string
  actorId: string
  correlationId: string
  remarks: string | null
  quantity: number
  lines?: {
    id: string
    sku: string
    stockLotId: string
    quantity: number
    rackId: string
    locationId: string | null
    destinationRackId: string | null
    destinationLocationId: string | null
    sourcePath: string
    destinationPath: string | null
  }[]
}
type Line = { sourceBalanceId: string; quantity: number; destination: string }
const titles: Record<Kind, string> = {
  SupplierReturn: 'Supplier returns',
  Disposal: 'Damage & disposal',
  Transfer: 'Warehouse transfers',
}
export default function StockTransactions({ kind }: { kind: Kind }) {
  const { id } = useParams(),
    [page, setPage] = useState(1),
    [creating, setCreating] = useState(false),
    [revision, setRevision] = useState(0),
    [success, setSuccess] = useState('')
  const r = useResource<Page<Transaction>>(
    `/api/v1/stock-transactions/${kind}?page=${page}&pageSize=20&revision=${revision}`,
  )
  return (
    <>
      <Heading
        title={titles[kind]}
        detail="Record completed physical movements. Posted history cannot be edited."
        action={
          !id && (
            <Button
              icon="plus"
              onClick={() => {
                setSuccess('')
                setCreating(true)
              }}
            >
              Record completed movement
            </Button>
          )
        }
      />
      <ErrorMessage error={r.error} />
      {success && (
        <p role="status" className="notice success">
          {success}
        </p>
      )}
      {id ? (
        <TransactionDetails kind={kind} id={id} />
      ) : creating ? (
        <TransactionForm
          kind={kind}
          onCancel={() => setCreating(false)}
          onDone={(number) => {
            setSuccess(`${number} recorded successfully.`)
            setCreating(false)
            setRevision((v) => v + 1)
          }}
        />
      ) : (
        <>
          <section className="panel">
            {r.loading ? (
              <LoadingSkeleton />
            ) : r.data?.items.length ? (
              <DataTable
                items={r.data.items}
                getKey={(t) => t.id}
                caption={titles[kind]}
                columns={[
                  {
                    key: 'number',
                    label: 'Reference',
                    render: (t) => (
                      <Link
                        className="font-semibold underline"
                        to={`${base(kind)}/${t.id}`}
                      >
                        {t.number}
                      </Link>
                    ),
                  },
                  {
                    key: 'date',
                    label: 'Completed at',
                    render: (t) => new Date(t.completedAt).toLocaleString(),
                  },
                  {
                    key: 'qty',
                    label: 'Whole units',
                    render: (t) => t.quantity.toLocaleString(),
                  },
                  {
                    key: 'remark',
                    label: 'Remarks',
                    render: (t) => t.remarks ?? '—',
                  },
                ]}
              />
            ) : (
              <EmptyState
                title="No completed movements"
                detail="Only record stock after the physical operation is complete."
              />
            )}
          </section>
          <Pagination
            page={page}
            total={r.data?.total ?? 0}
            noun="transactions"
            onPage={setPage}
          />
        </>
      )}
    </>
  )
}
function base(kind: Kind) {
  return kind === 'Transfer'
    ? '/transfers'
    : kind === 'Disposal'
      ? '/disposals'
      : '/supplier-returns'
}
function TransactionDetails({ kind, id }: { kind: Kind; id: string }) {
  const r = useResource<Transaction>(`/api/v1/stock-transactions/${kind}/${id}`)
  return (
    <>
      <ErrorMessage error={r.error} />
      {r.loading ? (
        <LoadingSkeleton />
      ) : (
        r.data && (
          <section className="panel p-3 space-y-3">
            <Link to={base(kind)}>← All transactions</Link>
            <h2 className="text-lg font-semibold">{r.data.number}</h2>
            <p>{new Date(r.data.completedAt).toLocaleString()}</p>
            <p>{r.data.remarks}</p>
            <p className="break-all text-sm">
              Actor: {r.data.actorId} · Correlation: {r.data.correlationId}
            </p>
            <DataTable
              items={r.data.lines ?? []}
              getKey={(l) => l.id}
              caption="Posted selections"
              columns={[
                {
                  key: 'sku',
                  label: 'Product / lot',
                  render: (l) => (
                    <div className="break-all">
                      <strong>{l.sku}</strong>
                      <p>{l.stockLotId}</p>
                    </div>
                  ),
                },
                {
                  key: 'source',
                  label: 'Source rack / position',
                  render: (l) => (
                    <div className="break-all">
                      {l.sourcePath}
                      <p className="field-hint">{l.rackId}</p>
                    </div>
                  ),
                },
                {
                  key: 'dest',
                  label: 'Destination',
                  render: (l) => (
                    <div className="break-all">
                      {l.destinationPath ?? 'Outside WROS'}
                    </div>
                  ),
                },
                {
                  key: 'qty',
                  label: 'Units',
                  render: (l) => l.quantity.toLocaleString(),
                },
              ]}
            />
          </section>
        )
      )}
    </>
  )
}
function TransactionForm({
  kind,
  onCancel,
  onDone,
}: {
  kind: Kind
  onCancel: () => void
  onDone: (number: string) => void
}) {
  const { user } = useSession()
  const [lines, setLines] = useState<Line[]>([
      { sourceBalanceId: '', quantity: 1, destination: '' },
    ]),
    [supplierId, setSupplierId] = useState(''),
    [remarks, setRemarks] = useState(''),
    [receipt, setReceipt] = useState(''),
    [po, setPo] = useState(''),
    [confirmed, setConfirmed] = useState(false),
    [busy, setBusy] = useState(false),
    [error, setError] = useState<unknown>(null),
    [requestKey] = useState(() => crypto.randomUUID()),
    [search, setSearch] = useState(''),
    [stockPage, setStockPage] = useState(1)
  const settledSearch = useDebouncedValue(search)
  const balances = useResource<Page<InventoryRow>>(
    `/api/v1/stock-transactions/balances?page=${stockPage}&pageSize=100&search=${encodeURIComponent(settledSearch)}`,
  )
  const destinations = useLookup<Placement>(
      kind === 'Transfer' ? '/api/v1/stock-transactions/placements' : null,
    ),
    suppliers = useLookup<{ id: string; code: string; name: string }>(
      kind === 'SupplierReturn' ? '/api/v1/stock-transactions/suppliers' : null,
    )
  const [selected, setSelected] = useState<Record<string, InventoryRow>>({})
  const { confirm, confirmation } = useConfirm()
  function update(index: number, patch: Partial<Line>) {
    setLines((v) => v.map((l, i) => (i === index ? { ...l, ...patch } : l)))
  }
  async function submit() {
    if (
      !(await confirm({
        title: 'Record completed physical movement?',
        message:
          'Stock will change atomically. This record cannot be edited. Confirm quantities and physical completion.',
        confirmLabel: 'Record movement',
        danger: true,
      }))
    )
      return
    setBusy(true)
    setError(null)
    try {
      const posted = await mutate<Transaction>(
        'POST',
        `/api/v1/stock-transactions/${kind}`,
        {
          idempotencyKey: requestKey,
          supplierId: supplierId || null,
          deliveryNoteId: receipt || null,
          purchaseOrderId: po || null,
          remarks,
          physicalCompletionConfirmed: confirmed,
          lines: lines.map((l) => {
            const dest = destinations.items.find(
              (d) => `${d.rackId}:${d.locationId ?? ''}` === l.destination,
            )
            return {
              sourceBalanceId: l.sourceBalanceId,
              quantity: l.quantity,
              destinationRackId: dest?.rackId ?? null,
              destinationLocationId: dest?.locationId ?? null,
            }
          }),
        },
      )
      onDone(posted.number)
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }
  return (
    <section className="panel p-3">
      <h2 className="mb-4 text-lg font-semibold">Record completed movement</h2>
      <ErrorMessage
        error={error || balances.error || destinations.error || suppliers.error}
      />
      <form
        onSubmit={(e) => {
          e.preventDefault()
          void submit()
        }}
      >
        <fieldset disabled={busy} className="space-y-3">
          <div className="grid gap-3 sm:grid-cols-2">
            {kind === 'SupplierReturn' && (
              <Field label="Supplier">
                <Select
                  required
                  value={supplierId}
                  onChange={(e) => setSupplierId(e.target.value)}
                >
                  <option value="">Choose supplier</option>
                  {suppliers.items.map((s) => (
                    <option key={s.id} value={s.id}>
                      {s.code} · {s.name}
                    </option>
                  ))}
                </Select>
              </Field>
            )}
            <Field label={kind === 'Disposal' ? 'Disposal reason' : 'Remarks'}>
              <Input
                required={kind === 'Disposal'}
                maxLength={2000}
                value={remarks}
                onChange={(e) => setRemarks(e.target.value)}
              />
            </Field>
            <Field label="Original posted DN ID (optional)">
              <Input
                value={receipt}
                onChange={(e) => setReceipt(e.target.value)}
              />
            </Field>
            <Field label="Original PO ID (optional)">
              <Input value={po} onChange={(e) => setPo(e.target.value)} />
            </Field>
          </div>
          <Field label="Find source stock (SKU, name, barcode or brand)">
            <Input
              value={search}
              onChange={(e) => {
                setSearch(e.target.value)
                setStockPage(1)
              }}
            />
          </Field>
          <Pagination
            page={stockPage}
            pageSize={100}
            total={balances.data?.total ?? 0}
            noun="source placements"
            onPage={setStockPage}
          />
          {lines.map((l, i) => {
            const options = Object.values(
              Object.fromEntries(
                [
                  ...Object.values(selected),
                  ...(balances.data?.items ?? []),
                ].map((b) => [b.id, b]),
              ),
            )
            const source = options.find((b) => b.id === l.sourceBalanceId)
            return (
              <div className="rounded-xl border p-3 space-y-3" key={i}>
                <div className="grid gap-3 md:grid-cols-2">
                  <Field label={`Source ${i + 1}: product / lot / location`}>
                    <Select
                      required
                      value={l.sourceBalanceId}
                      onChange={(e) => {
                        const b = options.find((v) => v.id === e.target.value)
                        if (b) setSelected((v) => ({ ...v, [b.id]: b }))
                        update(i, {
                          sourceBalanceId: e.target.value,
                          destination: '',
                        })
                      }}
                    >
                      <option value="">Choose stock</option>
                      {options.map((b) => (
                        <option key={b.id} value={b.id}>
                          {b.sku} ·{' '}
                          {b.supplierBatch ?? b.stockLotId.slice(0, 8)} ·{' '}
                          {b.path} · {b.quantity} units · expiry{' '}
                          {b.expiry ?? 'none'}
                        </option>
                      ))}
                    </Select>
                  </Field>
                  <Field label="Whole units">
                    <Input
                      required
                      type="number"
                      min={1}
                      step={1}
                      max={Number.MAX_SAFE_INTEGER}
                      value={l.quantity}
                      onChange={(e) =>
                        update(i, { quantity: e.target.valueAsNumber })
                      }
                    />
                  </Field>
                  {kind === 'Transfer' && (
                    <Field label="Destination in another warehouse">
                      <Select
                        required
                        value={l.destination}
                        onChange={(e) =>
                          update(i, { destination: e.target.value })
                        }
                      >
                        <option value="">Choose destination</option>
                        {destinations.items
                          .filter((d) => d.warehouseId !== source?.warehouseId)
                          .map((d) => (
                            <option
                              key={`${d.rackId}:${d.locationId ?? ''}`}
                              value={`${d.rackId}:${d.locationId ?? ''}`}
                            >
                              {d.path}
                            </option>
                          ))}
                      </Select>
                    </Field>
                  )}
                </div>
                <Button
                  variant="secondary"
                  disabled={lines.length === 1}
                  onClick={() => setLines((v) => v.filter((_, n) => n !== i))}
                >
                  Remove selection
                </Button>
              </div>
            )
          })}
          <Button
            variant="secondary"
            icon="plus"
            disabled={lines.length >= 500}
            onClick={() =>
              setLines((v) => [
                ...v,
                { sourceBalanceId: '', quantity: 1, destination: '' },
              ])
            }
          >
            Add selection / destination split
          </Button>
          <label className="flex min-h-11 items-center gap-3">
            <input
              required
              type="checkbox"
              checked={confirmed}
              onChange={(e) => setConfirmed(e.target.checked)}
            />
            The physical{' '}
            {kind === 'Transfer'
              ? 'move'
              : kind === 'Disposal'
                ? 'disposal'
                : 'supplier handover'}{' '}
            is complete.
          </label>
          <p className="field-hint">
            Signed in as {user?.userName}. Keep this form open after a network
            error and retry the same submission; its key prevents duplicate
            stock movement.
          </p>
          <div className="flex flex-wrap gap-3">
            <Button type="submit" loading={busy} disabled={!confirmed}>
              Record movement
            </Button>
            <Button variant="secondary" onClick={onCancel}>
              Cancel
            </Button>
          </div>
        </fieldset>
      </form>
      {confirmation}
    </section>
  )
}
