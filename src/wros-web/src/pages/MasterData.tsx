import { notifySuccess } from '../components/feedback/notifications'
import { useEffect, useState } from 'react'
import WarehouseHierarchy from './WarehouseHierarchy'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { get, mutate } from '../api/client'
import type { Page } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { ErrorMessage, Field, Heading } from '../components/Form'
import { Badge, Button, Select } from '../components/ui/Controls'
import { Dialog } from '../components/ui/Dialog'
import { DataTable } from '../components/data-display/DataTable'
import { Pagination } from '../components/data-display/Pagination'
import { EmptyState, LoadingSkeleton } from '../components/feedback/States'
import { useLookup } from '../hooks/useLookup'
import type { MasterRecord } from '../features/master-data/types'

export type MasterKind = 'suppliers' | 'products' | 'warehouses' | 'areas' | 'racks' | 'locations'
const definitions = {
  suppliers: { title: 'Suppliers', singular: 'supplier', permission: 'suppliers.manage', parent: '', child: '' },
  products: { title: 'Products', singular: 'product', permission: 'products.manage', parent: '', child: '' },
  warehouses: { title: 'Warehouses', singular: 'warehouse', permission: 'warehouses.manage', parent: '', child: 'areas' },
  areas: { title: 'Warehouse areas', singular: 'area', permission: 'warehouses.manage', parent: 'warehouses', child: 'racks' },
  racks: { title: 'Storage racks', singular: 'rack', permission: 'warehouses.manage', parent: 'areas', child: 'locations' },
  locations: { title: 'Bins, drawers & trays', singular: 'location', permission: 'warehouses.manage', parent: 'racks', child: '' },
} as const
export default function MasterData({ kind }: { kind: MasterKind }) {
  if (kind !== 'products' && kind !== 'suppliers') return <WarehouseHierarchy kind={kind} />
  return <MasterDataTable kind={kind} />
}
function MasterDataTable({ kind }: { kind: MasterKind }) {
  const definition = definitions[kind]; const { parentId } = useParams()
  const { user } = useSession(); const canManage = user?.permissions.includes(definition.permission)
  const [data, setData] = useState<Page<MasterRecord> | null>(null)
  const [page, setPage] = useState(1); const [search, setSearch] = useState(''); const [query, setQuery] = useState(''); const [active, setActive] = useState('')
  const [loading, setLoading] = useState(true); const [busy, setBusy] = useState(false); const [revision, setRevision] = useState(0)
  const [error, setError] = useState<unknown>(null);
  const [selected, setSelected] = useState<MasterRecord | 'new' | null>(null)
  const parentLookup = useLookup<MasterRecord>(definition.parent ? `/api/v1/${definition.parent}` : null)
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setError(null)
    get<Page<MasterRecord>>(`/api/v1/${kind}?page=${page}&pageSize=20&search=${encodeURIComponent(query)}${active ? `&active=${active}` : ''}${parentId ? `&parentId=${parentId}` : ''}`, controller.signal)
      .then(setData).catch(failure => { if (!controller.signal.aborted) setError(failure) }).finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [kind, parentId, page, query, active, revision])
  const item = selected && selected !== 'new' ? selected : null
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); const values = new FormData(event.currentTarget); setBusy(true); setError(null)
    const body = { code: values.get('code'), name: values.get('name'), description: values.get('description'), isActive: values.has('active'), rowVersion: item?.rowVersion,
      parentId: item?.parentId ?? parentId ?? values.get('parentId') ?? null,
      ...Object.fromEntries(['contactPerson', 'phone', 'email', 'address', 'remarks', 'barcode', 'brand', 'externalProductId'].map(key => [key, values.get(key) || null])), trackExpiry: values.has('trackExpiry') }
    try {
      await mutate(item ? 'PUT' : 'POST', `/api/v1/${kind}${item ? `/${item.id}` : ''}`, body)
      setSelected(null); setRevision(value => value + 1); notifySuccess(`${definition.singular[0].toUpperCase() + definition.singular.slice(1)} saved.`)
    } catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  const parent = parentLookup.items.find(value => value.id === parentId)
  return <><Heading eyebrow="Master data" title={definition.title} detail={parent ? `${parent.parentPath ? `${parent.parentPath} / ` : ''}${parent.code} · ${parent.name}` : 'Maintain the reference data your warehouse team relies on.'} action={canManage && <Button icon="plus" onClick={() => { setSelected('new'); setError(null) }}>Create {definition.singular}</Button>} />
    {parentId && <Link className="text-button mb-4" to={`/${definition.parent}`}>Back to {definition.parent}</Link>}
    {!selected && <ErrorMessage error={error} />}
    <section className="panel"><div className="toolbar"><form className="search-form" onSubmit={event => { event.preventDefault(); setPage(1); setQuery(search) }}><input aria-label="Search code or name" placeholder="Search code or name…" value={search} maxLength={100} onChange={event => setSearch(event.target.value)} /><Button type="submit" variant="secondary">Search</Button></form>
      <Select aria-label="Filter status" className="status-filter" value={active} onChange={event => { setActive(event.target.value); setPage(1) }}><option value="">All statuses</option><option value="true">Active</option><option value="false">Inactive</option></Select><Button variant="ghost" icon="refresh" disabled={loading} onClick={() => setRevision(value => value + 1)}>Refresh</Button></div>
      {loading ? <LoadingSkeleton label={`Loading ${definition.title}…`} /> : data?.items.length ? <DataTable items={data.items} getKey={value => value.id} caption={definition.title} columns={[
        { key: 'code', label: kind === 'products' ? 'SKU / product' : 'Code / name', render: value => <div><strong>{value.code}</strong><p className="mt-1">{value.name}</p></div> },
        { key: 'detail', label: kind === 'products' ? 'Product details' : 'Details', render: value => <span>{kind === 'products' ? [value.brand, value.barcode, value.trackExpiry ? 'Expiry tracked' : 'No expiry requirement'].filter(Boolean).join(' · ') : value.parentPath ?? value.description ?? '—'}</span> },
        { key: 'status', label: 'Status', render: value => <Badge tone={value.isActive ? 'success' : 'neutral'}>{value.isActive ? 'Active' : 'Inactive'}</Badge> },
        { key: 'actions', label: 'Actions', render: value => <div className="flex flex-wrap gap-1"><button className="text-button" onClick={() => { setSelected(value); setError(null) }}>{canManage ? 'Edit' : 'View'}<span className="sr-only"> {value.name}</span></button>{definition.child && <Link className="text-button" to={`/${kind}/${value.id}/${definition.child}`}>{definition.child}</Link>}</div> },
      ]} /> : <EmptyState title="No records found" detail={error ? 'Use Refresh to retry the connection.' : 'Adjust the search or create your first record.'} />}</section>
    <Pagination page={page} total={data?.total ?? 0} noun={definition.title.toLowerCase()} disabled={loading} onPage={setPage} />
    <Dialog open={!!selected} title={`${selected === 'new' ? 'Create' : canManage ? 'Edit' : 'View'} ${definition.singular}`} description="Changes are audited. Deactivation preserves history." busy={busy} onClose={() => { setSelected(null); setError(null) }}>
      <ErrorMessage error={error} /><ErrorMessage error={definition.parent ? parentLookup.error : null} />
      {selected && <form key={item?.id ?? 'new'} onSubmit={save} className="space-y-3" aria-busy={busy}><fieldset disabled={!canManage || busy} className="space-y-3">
        {definition.parent && <Field label="Parent"><Select name="parentId" defaultValue={item?.parentId ?? parentId ?? ''} disabled={!!item || !!parentId} required><option value="">Choose {definition.parent}</option>{parentLookup.items.map(value => <option key={value.id} value={value.id}>{value.parentPath ? `${value.parentPath} / ` : ''}{value.code} · {value.name}{!value.isActive ? ' (inactive)' : ''}</option>)}</Select></Field>}
        <div className="form-grid"><Field label={kind === 'products' ? 'SKU' : 'Code'}><input name="code" maxLength={64} required defaultValue={item?.code} /></Field><Field label="Name"><input name="name" maxLength={200} required defaultValue={item?.name} /></Field></div>
        <Field label="Description"><input name="description" maxLength={2000} defaultValue={item?.description ?? ''} /></Field>
        {kind === 'suppliers' && <div className="form-grid">{(['contactPerson', 'phone', 'email', 'address', 'remarks'] as const).map(key => <Field key={key} label={key === 'contactPerson' ? 'Contact person' : key[0].toUpperCase() + key.slice(1)}><input name={key} type={key === 'email' ? 'email' : key === 'phone' ? 'tel' : 'text'} maxLength={key === 'phone' ? 50 : key === 'email' ? 254 : key === 'contactPerson' ? 200 : 2000} defaultValue={item?.[key] ?? ''} /></Field>)}</div>}
        {kind === 'products' && <><div className="form-grid">{(['barcode', 'brand', 'externalProductId'] as const).map(key => <Field key={key} label={key === 'externalProductId' ? 'External product ID (optional)' : key === 'barcode' ? 'Barcode (optional)' : 'Brand (optional)'}><input name={key} maxLength={key === 'brand' ? 200 : 100} defaultValue={item?.[key] ?? ''} /></Field>)}</div><label className="check-label"><input name="trackExpiry" type="checkbox" defaultChecked={item?.trackExpiry} />Require expiry on received lots</label><p className="field-hint">Whole units only. Creating a product never adds stock. External IDs are references; no e-commerce synchronization is performed.</p></>}
        <label className="check-label"><input name="active" type="checkbox" defaultChecked={item?.isActive ?? true} />Active</label>
      </fieldset><div className="dialog-actions"><Button variant="secondary" disabled={busy} onClick={() => setSelected(null)}>Close</Button>{canManage && <Button type="submit" loading={busy}>Save {definition.singular}</Button>}</div></form>}
    </Dialog>
  </>
}
