import { notifySuccess } from '../components/feedback/notifications'
import { useState } from 'react'
import { mutate } from '../api/client'
import type { Page } from '../api/types'
import { useResource } from '../hooks/useResource'
import { ErrorMessage, Field, Heading } from '../components/Form'
import { Button, Input, Select, Badge } from '../components/ui/Controls'
import { DataTable } from '../components/data-display/DataTable'
import { Pagination } from '../components/data-display/Pagination'
import { LoadingSkeleton, EmptyState } from '../components/feedback/States'
type Client = {
  id: string
  name: string
  isActive: boolean
  rowVersion: string
}
type Mapping = {
  id: string
  clientId: string
  externalProductId: string
  productId: string
  sku: string
  product: string
  isActive: boolean
  rowVersion: string
}
type Status = {
  audience: string
  snapshotMinutes: number
  maxSnapshotRows: number
  clients: Client[]
  configured: { id: string; scopes: string[]; keyIds: string[] }[]
  recentDeductions: {
    id: string
    clientId: string
    externalOrderId: string
    eventId: string
    completedAt: string
    quantity: number
    correlationId: string
  }[]
  totalDeductions: number
  recentSnapshots: {
    id: string
    clientId: string
    version: number
    createdAt: string
    expiresAt: string
    totalRows: number
  }[]
}
export default function Integration() {
  const [page, setPage] = useState(1),
    [deductionPage, setDeductionPage] = useState(1),
    [revision, setRevision] = useState(0),
    [error, setError] = useState<unknown>(null),
    [busy, setBusy] = useState(false),
    [client, setClient] = useState<Client>({
      id: '',
      name: '',
      isActive: true,
      rowVersion: '',
    }),
    [mapping, setMapping] = useState<Partial<Mapping>>({
      clientId: '',
      externalProductId: '',
      productId: '',
      isActive: true,
    })
  const status = useResource<Status>(
      `/api/v1/integration-management?page=${deductionPage}&pageSize=20&revision=${revision}`,
    ),
    mappings = useResource<Page<Mapping>>(
      `/api/v1/integration-management/mappings?page=${page}&pageSize=20&revision=${revision}`,
    )
  async function saveClient() {
    setBusy(true)
    setError(null)
    try {
      await mutate('POST', '/api/v1/integration-management/clients', client)
      setRevision((v) => v + 1)
      setClient({ id: '', name: '', isActive: true, rowVersion: '' })
      notifySuccess('Client saved successfully.')
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }
  async function saveMapping() {
    setBusy(true)
    setError(null)
    try {
      await mutate(
        mapping.id ? 'PUT' : 'POST',
        `/api/v1/integration-management/mappings${mapping.id ? '/' + mapping.id : ''}`,
        mapping,
      )
      setRevision((v) => v + 1)
      notifySuccess('Changes saved successfully.')
      setMapping({
        clientId: '',
        externalProductId: '',
        productId: '',
        isActive: true,
      })
    } catch (e) {
      setError(e)
    } finally {
      setBusy(false)
    }
  }
  return (
    <>
      <Heading
        title="E-commerce integration"
        detail="Separate machine identities, stable product mappings and immutable inventory snapshots. Legacy e-commerce remains independent."
      />
      <ErrorMessage error={error || status.error || mappings.error} />

      {status.loading ? (
        <LoadingSkeleton />
      ) : (
        status.data && (
          <>
            <section className="panel p-3">
              <h2 className="mb-3 text-lg font-semibold">
                Connection contract
              </h2>
              <p className="break-all">
                Audience: <code>{status.data.audience}</code>
              </p>
              <p>
                Snapshots expire after {status.data.snapshotMinutes} minutes ·
                capacity {status.data.maxSnapshotRows.toLocaleString()}{' '}
                placements.
              </p>
              <p className="mt-3 field-hint">
                HTTPS and a fresh RS256 signed assertion per request are
                required. Private keys never belong in this browser. Configure
                public keys and least-privilege scopes in secure server
                configuration. Full authentication, curl examples, snapshot
                validation and recovery are in docs/e-commerce-integration.md.
              </p>
              <details className="mt-4">
                <summary className="cursor-pointer py-3">
                  Machine API endpoints
                </summary>
                <ul className="space-y-3 break-all text-sm">
                  <li>
                    POST /api/v1/integration/snapshots · inventory.snapshot
                  </li>
                  <li>
                    GET
                    /api/v1/integration/snapshots/:id?page=1&amp;pageSize=100 ·
                    inventory.snapshot
                  </li>
                  <li>
                    POST /api/v1/integration/ready-to-deliver · orders.deduct
                  </li>
                </ul>
                <p className="mt-4">
                  Stage every snapshot page, validate count/hash and increasing
                  version, then publish atomically. Finalize Ready to Deliver
                  only after WROS confirms deduction. Retry timeouts with the
                  same event key and a fresh assertion.
                </p>
              </details>
            </section>
            <section className="panel p-3">
              <h2 className="mb-4 text-lg font-semibold">Client registry</h2>
              <DataTable
                items={status.data.clients}
                getKey={(c) => c.id}
                caption="Integration clients"
                columns={[
                  {
                    key: 'id',
                    label: 'Client',
                    render: (c) => (
                      <>
                        <strong>{c.name}</strong>
                      </>
                    ),
                  },
                  {
                    key: 'status',
                    label: 'Status',
                    render: (c) => (
                      <Badge>{c.isActive ? 'Active' : 'Disabled'}</Badge>
                    ),
                  },
                  {
                    key: 'config',
                    label: 'Configured scopes',
                    render: (c) =>
                      status.data?.configured
                        .find((v) => v.id === c.id)
                        ?.scopes.join(', ') ?? 'No permitted key configured',
                  },
                  {
                    key: 'action',
                    label: 'Action',
                    render: (c) => (
                      <Button variant="secondary" onClick={() => setClient(c)}>
                        Edit client
                      </Button>
                    ),
                  },
                ]}
              />
              <form
                className="mt-5 space-y-3"
                onSubmit={(e) => {
                  e.preventDefault()
                  void saveClient()
                }}
              >
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Client ID">
                    <Input
                      required
                      maxLength={80}
                      pattern="[A-Za-z0-9._-]+"
                      value={client.id}
                      readOnly={!!client.rowVersion}
                      onChange={(e) =>
                        setClient((v) => ({ ...v, id: e.target.value }))
                      }
                    />
                  </Field>
                  <Field label="Display name">
                    <Input
                      required
                      maxLength={200}
                      value={client.name}
                      onChange={(e) =>
                        setClient((v) => ({ ...v, name: e.target.value }))
                      }
                    />
                  </Field>
                </div>
                <label className="flex min-h-11 items-center gap-3">
                  <input
                    type="checkbox"
                    checked={client.isActive}
                    onChange={(e) =>
                      setClient((v) => ({ ...v, isActive: e.target.checked }))
                    }
                  />
                  Client enabled (also requires server key configuration)
                </label>
                <Button type="submit" loading={busy}>
                  Save client
                </Button>
              </form>
            </section>
            <section className="panel p-3">
              <h2 className="mb-4 text-lg font-semibold">
                Product identity mappings
              </h2>
              <p className="mb-4 field-hint">
                A client external identifier maps to one WROS product. Several
                external variant identifiers may reference one product; they
                share the same physical stock and must not be summed as
                independent availability.
              </p>
              {mappings.data?.items.length ? (
                <DataTable
                  items={mappings.data.items}
                  getKey={(m) => m.id}
                  caption="Product mappings"
                  columns={[
                    {
                      key: 'external',
                      label: 'Client / external ID',
                      render: (m) => (
                        <div className="break-all">
                          <strong>{status.data?.clients.find(client => client.id === m.clientId)?.name ?? 'Integration client'}</strong>
                          <p>{m.externalProductId}</p>
                        </div>
                      ),
                    },
                    {
                      key: 'product',
                      label: 'WROS product',
                      render: (m) => (
                        <>
                          <strong>{m.sku}</strong>
                          <p>{m.product}</p>
                        </>
                      ),
                    },
                    {
                      key: 'status',
                      label: 'Status',
                      render: (m) => (
                        <Badge>{m.isActive ? 'Active' : 'Disabled'}</Badge>
                      ),
                    },
                    {
                      key: 'action',
                      label: 'Action',
                      render: (m) => (
                        <Button
                          variant="secondary"
                          onClick={() => setMapping(m)}
                        >
                          Edit mapping
                        </Button>
                      ),
                    },
                  ]}
                />
              ) : (
                <EmptyState
                  title="No product mappings"
                  detail="Create explicit identifier mappings before sending order deductions."
                />
              )}
              <Pagination
                page={page}
                total={mappings.data?.total ?? 0}
                noun="mappings"
                onPage={setPage}
              />
              <form
                className="mt-5 space-y-3"
                onSubmit={(e) => {
                  e.preventDefault()
                  void saveMapping()
                }}
              >
                <div className="grid gap-3 sm:grid-cols-2">
                  <Field label="Client">
                    <Select
                      required
                      value={mapping.clientId}
                      onChange={(e) =>
                        setMapping((v) => ({ ...v, clientId: e.target.value }))
                      }
                    >
                      <option value="">Choose client</option>
                      {status.data.clients.map((c) => (
                        <option key={c.id} value={c.id}>
                          {c.name}
                        </option>
                      ))}
                    </Select>
                  </Field>
                  <Field label="External product ID" width="wide">
                    <Input
                      required
                      maxLength={100}
                      value={mapping.externalProductId}
                      onChange={(e) =>
                        setMapping((v) => ({
                          ...v,
                          externalProductId: e.target.value,
                        }))
                      }
                    />
                  </Field>
                  <Field label="Stable WROS product ID" width="wide">
                    <Input
                      required
                      value={mapping.productId}
                      onChange={(e) =>
                        setMapping((v) => ({ ...v, productId: e.target.value }))
                      }
                    />
                  </Field>
                </div>
                <label className="flex min-h-11 items-center gap-3">
                  <input
                    type="checkbox"
                    checked={mapping.isActive ?? true}
                    onChange={(e) =>
                      setMapping((v) => ({ ...v, isActive: e.target.checked }))
                    }
                  />
                  Mapping active
                </label>
                <div className="flex flex-wrap gap-3">
                  <Button type="submit" loading={busy}>
                    Save mapping
                  </Button>
                  <Button
                    variant="secondary"
                    onClick={() =>
                      setMapping({
                        clientId: '',
                        externalProductId: '',
                        productId: '',
                        isActive: true,
                      })
                    }
                  >
                    New mapping
                  </Button>
                </div>
              </form>
            </section>
            <section className="panel p-3">
              <h2 className="mb-4 text-lg font-semibold">Recent deductions</h2>
              <DataTable
                items={status.data.recentDeductions}
                getKey={(d) => d.id}
                caption="Ready to Deliver deductions"
                columns={[
                  {
                    key: 'order',
                    label: 'Client / order',
                    render: (d) => (
                      <>
                        <strong>{status.data?.clients.find(client => client.id === d.clientId)?.name ?? 'Integration client'}</strong>
                        <p>{d.externalOrderId}</p>
                      </>
                    ),
                  },
                  {
                    key: 'event',
                    label: 'Event',
                    render: (d) => (
                      <div className="break-all">
                        {d.eventId}

                      </div>
                    ),
                  },
                  {
                    key: 'qty',
                    label: 'Units',
                    render: (d) => d.quantity.toLocaleString(),
                  },
                  {
                    key: 'date',
                    label: 'Completed',
                    render: (d) => new Date(d.completedAt).toLocaleString(),
                  },
                ]}
              />
              <Pagination
                page={deductionPage}
                total={status.data.totalDeductions}
                noun="deductions"
                onPage={setDeductionPage}
              />
            </section>
            <section className="panel p-3">
              <h2 className="mb-4 text-lg font-semibold">Recent snapshots</h2>
              <DataTable
                items={status.data.recentSnapshots}
                getKey={(s) => s.id}
                caption="Latest 20 immutable snapshots"
                columns={[
                  {
                    key: 'id',
                    label: 'Snapshot / client',
                    render: (s) => (
                      <div className="break-all">
                        <strong>{status.data?.clients.find(client => client.id === s.clientId)?.name ?? 'Integration client'}</strong>

                      </div>
                    ),
                  },
                  {
                    key: 'version',
                    label: 'Version / rows',
                    render: (s) => (
                      <>
                        {s.version} · {s.totalRows} rows
                      </>
                    ),
                  },
                  {
                    key: 'expiry',
                    label: 'Expires',
                    render: (s) => new Date(s.expiresAt).toLocaleString(),
                  },
                ]}
              />
            </section>
          </>
        )
      )}
    </>
  )
}
