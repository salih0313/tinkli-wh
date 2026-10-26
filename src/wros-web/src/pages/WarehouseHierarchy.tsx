import { notifySuccess } from '../components/feedback/notifications'
import { useEffect, useId, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router-dom'
import { get, mutate } from '../api/client'
import { useSession } from '../auth/SessionContext'
import { ErrorMessage, Field, Heading } from '../components/Form'
import { Badge, Button, IconButton, Select } from '../components/ui/Controls'
import { Icon } from '../components/ui/Icon'
import { Dialog } from '../components/ui/Dialog'
import { EmptyState, LoadingSkeleton } from '../components/feedback/States'
import { useLookup } from '../hooks/useLookup'
import { useConfirm } from '../hooks/useConfirm'
import type { MasterRecord } from '../features/master-data/types'

export type HierarchyKind = 'warehouses' | 'areas' | 'racks' | 'locations'
const levels: HierarchyKind[] = ['warehouses', 'areas', 'racks', 'locations']
const labels = { warehouses: 'warehouse', areas: 'area', racks: 'rack', locations: 'location' }
const titles = { warehouses: 'Warehouses', areas: 'Areas', racks: 'Racks', locations: 'Locations' }
interface WarehouseCount { id: string; areas: number; racks: number; locations: number }
interface Structure { warehouse: MasterRecord; areas: MasterRecord[]; racks: MasterRecord[]; locations: MasterRecord[] }
type Editor = { kind: HierarchyKind; item?: MasterRecord; parent?: MasterRecord }
type Actions = { structure?: Structure | null; canManage: boolean; revision: number; busy: boolean; edit: (value: Editor) => void; toggle: (kind: HierarchyKind, item: MasterRecord) => void }

function Branch({ item, kind, actions }: { item: MasterRecord; kind: HierarchyKind; actions: Actions }) {
  const [expanded, setExpanded] = useState(true)
  const child = levels[levels.indexOf(kind) + 1]
  const contentId = useId()
  return <li className={`hierarchy-node hierarchy-${kind}`}>
    <div className="hierarchy-row">
      {child ? <IconButton label={`${expanded ? 'Collapse' : 'Expand'} ${item.name}`} icon={expanded ? 'down' : 'chevron'} aria-expanded={expanded} aria-controls={contentId} onClick={() => setExpanded(value => !value)} /> : <span className="hierarchy-leaf"><Icon name="overview" /></span>}
      <div className="hierarchy-identity"><span className="hierarchy-type">{labels[kind]}</span><strong>{item.name}</strong><span className="hierarchy-code">{item.code}</span>{item.description && <p>{item.description}</p>}</div>
      <div className="hierarchy-actions">
        {child && actions.canManage && <Button variant="secondary" icon="plus" onClick={() => { setExpanded(true); actions.edit({ kind: child, parent: item }) }}>Add {labels[child]}</Button>}
        <Button variant="ghost" onClick={() => actions.edit({ kind, item })} aria-label={`${actions.canManage ? 'Edit' : 'View'} ${item.name}`}>{actions.canManage ? 'Edit' : 'View'}</Button>
        {actions.canManage && <Button variant="ghost" disabled={actions.busy} className={item.isActive ? 'hierarchy-deactivate' : ''} onClick={() => actions.toggle(kind, item)} aria-label={`${item.isActive ? 'Deactivate' : 'Reactivate'} ${item.name}`}>{item.isActive ? 'Deactivate' : 'Reactivate'}</Button>}
      </div>
      <Badge tone={item.isActive ? 'success' : 'neutral'}>{item.isActive ? 'Active' : 'Inactive'}</Badge>
    </div>
    {child && <div id={contentId} hidden={!expanded} className="hierarchy-children">{expanded && <BranchList kind={child} parent={item} actions={actions} />}</div>}
  </li>
}
function BranchList({ kind, parent, actions }: { kind: HierarchyKind; parent?: MasterRecord; actions: Actions }) {
  if (actions.structure && kind !== 'warehouses') {
    const items = actions.structure[kind].filter(item => item.parentId === parent?.id)
    return <BranchItems items={items} kind={kind} parent={parent} actions={actions} />
  }
  return <RemoteBranchList kind={kind} parent={parent} actions={actions} />
}
function RemoteBranchList({ kind, parent, actions }: { kind: HierarchyKind; parent?: MasterRecord; actions: Actions }) {
  const lookup = useLookup<MasterRecord>(`/api/v1/${kind}${parent ? `?parentId=${parent.id}` : ''}`, actions.revision)
  if (lookup.loading && !lookup.items.length) return <LoadingSkeleton label={`Loading ${titles[kind].toLowerCase()}…`} />
  if (lookup.error) return <div className="hierarchy-empty"><ErrorMessage error={lookup.error} /><Button variant="secondary" icon="refresh" onClick={lookup.refresh}>Retry</Button></div>
  return <BranchItems items={lookup.items} kind={kind} parent={parent} actions={actions} />
}
function BranchItems({ items, kind, parent, actions }: { items: MasterRecord[]; kind: HierarchyKind; parent?: MasterRecord; actions: Actions }) {
  if (!items.length) return <div className="hierarchy-empty"><p>No {titles[kind].toLowerCase()} here yet.</p>{actions.canManage && <Button variant="secondary" icon="plus" onClick={() => actions.edit({ kind, parent })}>Add {labels[kind]}</Button>}</div>
  return <ul className="hierarchy-list" aria-label={`${titles[kind]}${parent ? ` in ${parent.name}` : ''}`}>{items.map(item => <Branch key={item.id} kind={kind} item={item} actions={actions} />)}</ul>
}
export default function WarehouseHierarchy({ kind }: { kind: HierarchyKind }) {
  const { parentId, warehouseId } = useParams()
  const overview = kind === 'warehouses' && !warehouseId
  const { user } = useSession()
  const canManage = !!user?.permissions.includes('warehouses.manage')
  const parentKind = levels[levels.indexOf(kind) - 1]
  const parents = useLookup<MasterRecord>(parentKind ? `/api/v1/${parentKind}` : null)
  const roots = useLookup<MasterRecord>(warehouseId ? null : `/api/v1/${kind}${parentId ? `?parentId=${parentId}` : ''}`)
  const [editor, setEditor] = useState<Editor | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const [search, setSearch] = useState('')
  const [status, setStatus] = useState('')
  const [revision, setRevision] = useState(0)
  const [counts, setCounts] = useState<WarehouseCount[]>([])
  const [loadedStructure, setStructure] = useState<Structure | null>(null)
  const structure = loadedStructure?.warehouse.id === warehouseId ? loadedStructure : null
  const [networkLoading, setNetworkLoading] = useState(true)
  const [networkError, setNetworkError] = useState<unknown>(null)
  useEffect(() => {
    const controller = new AbortController(); setNetworkLoading(true); setNetworkError(null)
    const request = warehouseId
      ? get<Structure>(`/api/v1/warehouses/${warehouseId}/structure`, controller.signal).then(setStructure)
      : overview ? get<WarehouseCount[]>('/api/v1/warehouses/counts', controller.signal).then(setCounts) : Promise.resolve()
    request.catch(failure => { if (!controller.signal.aborted) setNetworkError(failure) })
      .finally(() => { if (!controller.signal.aborted) setNetworkLoading(false) })
    return () => controller.abort()
  }, [warehouseId, overview, revision])
  const { confirm, confirmation } = useConfirm()
  const parent = parents.items.find(value => value.id === parentId)
  const visible = roots.items.filter(item => `${item.code} ${item.name}`.toLowerCase().includes(search.toLowerCase()) && (!status || String(item.isActive) === status))
  function refresh() { roots.refresh(); setRevision(value => value + 1) }
  function edit(value: Editor) { setError(null); setEditor(value) }
  async function toggle(nodeKind: HierarchyKind, item: MasterRecord) {
    if (!await confirm({ title: `${item.isActive ? 'Deactivate' : 'Reactivate'} ${labels[nodeKind]}?`, message: `${item.code} · ${item.name}. Existing history is preserved. This changes only this record; child statuses are not changed.`, confirmLabel: item.isActive ? 'Deactivate' : 'Reactivate' })) return
    setBusy(true); setError(null)
    try { await mutate('PUT', `/api/v1/${nodeKind}/${item.id}`, { ...item, isActive: !item.isActive }); refresh(); notifySuccess(`${item.name} ${item.isActive ? 'deactivated' : 'reactivated'}.`) }
    catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!editor) return
    const values = new FormData(event.currentTarget); setBusy(true); setError(null)
    try {
      await mutate(editor.item ? 'PUT' : 'POST', `/api/v1/${editor.kind}${editor.item ? `/${editor.item.id}` : ''}`, {
        code: values.get('code'), name: values.get('name'), description: values.get('description') || null,
        isActive: values.has('active'), parentId: editor.item?.parentId ?? editor.parent?.id ?? values.get('parentId') ?? null, rowVersion: editor.item?.rowVersion,
      })
      setEditor(null); refresh(); notifySuccess(`${titles[editor.kind].slice(0, -1)} saved.`)
    } catch (failure) { setError(failure) } finally { setBusy(false) }
  }
  const actions: Actions = { structure: warehouseId ? structure : null, canManage, revision, busy, edit, toggle }
  const editorParentKind = editor ? levels[levels.indexOf(editor.kind) - 1] : undefined
  return <>
    <Heading eyebrow="Warehouse setup" title={warehouseId ? structure?.warehouse.name ?? 'Warehouse structure' : kind === 'warehouses' ? 'Warehouses' : titles[kind]} detail={warehouseId ? `${structure?.warehouse.code ?? ''} · Manage areas, racks and locations` : parent ? `${parent.code} · ${parent.name}` : 'Your storage network at a glance. Open a warehouse to manage its structure.'} action={canManage && <Button icon="plus" disabled={warehouseId ? !structure : !!parentId && !parent} onClick={() => edit(warehouseId && structure ? { kind: 'areas', parent: structure.warehouse } : { kind, parent })}>Add {warehouseId ? 'area' : labels[kind]}</Button>} />
    {(warehouseId || kind !== 'warehouses') && <Link to="/warehouses" className="text-button mb-4">View warehouse network</Link>}
    {!editor && <ErrorMessage error={error} />}<ErrorMessage error={parents.error} />

    {warehouseId ? <section className="panel compact-tree" aria-label="Warehouse structure">
      <div className="tree-toolbar"><span>Areas / Racks / Locations</span><Button variant="ghost" icon="refresh" disabled={networkLoading} onClick={refresh}>Refresh</Button></div>
      <ErrorMessage error={networkError} />
      {networkLoading && !structure ? <LoadingSkeleton label="Loading warehouse structure…" /> : structure && <ul className="hierarchy-list hierarchy-roots"><Branch key={structure.warehouse.id} kind="warehouses" item={structure.warehouse} actions={actions} /></ul>}
    </section> : <section aria-label={titles[kind]}>
      <div className="hierarchy-toolbar"><div className="search-control"><Icon name="search" /><input aria-label={`Search ${titles[kind].toLowerCase()} by code or name`} placeholder={`Search ${titles[kind].toLowerCase()} by code or name…`} value={search} onChange={event => setSearch(event.target.value)} /></div><Select className="status-filter" aria-label="Filter status" value={status} onChange={event => setStatus(event.target.value)}><option value="">All statuses</option><option value="true">Active</option><option value="false">Inactive</option></Select><Button variant="secondary" icon="refresh" disabled={roots.loading || networkLoading} onClick={refresh}>Refresh</Button></div>
      <ErrorMessage error={networkError} />
      {roots.loading && !roots.items.length ? <LoadingSkeleton label={`Loading ${titles[kind].toLowerCase()}…`} /> : roots.error ? <ErrorMessage error={roots.error} /> : visible.length ? overview ? <div className="warehouse-card-grid">{visible.map(item => {
        const count = counts.find(value => value.id === item.id)
        return <article className="panel warehouse-card" key={item.id}><div className="warehouse-card-heading"><span className="warehouse-card-icon"><Icon name="warehouse" /></span><Badge tone={item.isActive ? 'success' : 'neutral'}>{item.isActive ? 'Active' : 'Inactive'}</Badge></div>
          <h2><Link to={`/warehouses/${item.id}/manage`}>{item.name}</Link></h2><p className="hierarchy-code">{item.code}</p>
          {item.description && <p className="warehouse-card-description">{item.description}</p>}
          <dl className="warehouse-card-counts">{(['areas', 'racks', 'locations'] as const).map(key => <div key={key}><dd>{networkLoading ? '…' : count?.[key] ?? '—'}</dd><dt>{titles[key]}</dt></div>)}</dl>
          <Link className="warehouse-manage-link" to={`/warehouses/${item.id}/manage`}>{canManage ? 'Manage warehouse' : 'View structure'}<Icon name="arrow" /></Link>
        </article>
      })}</div> : <div className="compact-tree panel"><ul className="hierarchy-list hierarchy-roots">{visible.map(item => <Branch key={item.id} kind={kind} item={item} actions={actions} />)}</ul></div> : <EmptyState title={`No ${titles[kind].toLowerCase()} found`} detail="Adjust your filters or add a record to get started." />}
      {overview && <p className="hierarchy-count mt-3">Counts include active and inactive areas, racks and locations.</p>}
    </section>}
    <Dialog open={!!editor} title={`${editor?.item ? canManage ? 'Edit' : 'View' : 'Add'} ${editor ? labels[editor.kind] : ''}`} description={editor?.parent ? `Inside ${editor.parent.code} · ${editor.parent.name}` : editor?.item?.parentPath ?? 'Changes are audited. Existing history is preserved.'} busy={busy} onClose={() => { setEditor(null); setError(null) }}>
      <ErrorMessage error={error} />
      {editor && <form key={editor.item?.id ?? `${editor.kind}-${editor.parent?.id ?? 'new'}`} onSubmit={save} className="space-y-3"><fieldset disabled={busy || !canManage} className="space-y-3">
        {editorParentKind && !editor.parent && !editor.item && <Field label={labels[editorParentKind]}><Select name="parentId" required defaultValue=""><option value="">Choose {labels[editorParentKind]}</option>{parents.items.map(value => <option key={value.id} value={value.id}>{value.parentPath ? `${value.parentPath} / ` : ''}{value.code} · {value.name}{value.isActive ? '' : ' (inactive)'}</option>)}</Select></Field>}
        <div className="form-grid"><Field label="Code"><input name="code" required maxLength={64} defaultValue={editor.item?.code} autoFocus /></Field><Field label="Name"><input name="name" required maxLength={200} defaultValue={editor.item?.name} /></Field></div>
        <Field label="Description (optional)"><textarea name="description" maxLength={2000} defaultValue={editor.item?.description ?? ''} /></Field>
        <label className="check-label"><input type="checkbox" name="active" defaultChecked={editor.item?.isActive ?? true} />Active</label>
      </fieldset><div className="dialog-actions"><Button variant="secondary" disabled={busy} onClick={() => setEditor(null)}>Cancel</Button>{canManage && <Button type="submit" loading={busy}>Save {labels[editor.kind]}</Button>}</div></form>}
    </Dialog>{confirmation}
  </>
}
