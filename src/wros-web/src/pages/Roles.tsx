import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { get, mutate } from '../api/client'
import type { Role } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { ErrorMessage, Heading } from '../components/Form'

export default function Roles() {
  const { refresh } = useSession()
  const [roles, setRoles] = useState<Role[]>([])
  const [permissions, setPermissions] = useState<string[]>([])
  const [revision, setRevision] = useState(0)
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [success, setSuccess] = useState('')
  useEffect(() => {
    const abort = new AbortController(); setLoading(true); setError(null)
    Promise.all([get<Role[]>('/api/v1/roles', abort.signal), get<string[]>('/api/v1/roles/permissions', abort.signal)])
      .then(([items, grants]) => { setRoles(items); setPermissions(grants) })
      .catch(failure => { if (!abort.signal.aborted) setError(failure) })
      .finally(() => { if (!abort.signal.aborted) setLoading(false) })
    return () => abort.abort()
  }, [revision])
  async function save(event: FormEvent<HTMLFormElement>, role: Role) {
    event.preventDefault()
    const values = new FormData(event.currentTarget)
    setBusy(true); setError(null); setSuccess('')
    try {
      await mutate('PUT', `/api/v1/roles/${role.id}/permissions`, { permissions: values.getAll('permissions'), concurrencyStamp: role.concurrencyStamp })
      setSuccess(`${role.name} permissions saved. Changes apply to the next API request.`); setRevision(value => value + 1); await refresh()
    } catch (failure) { setError(failure) }
    finally { setBusy(false) }
  }
  return <><Heading title="Roles & permissions" detail="Manage grants for the six defined roles. User and role administration always require SuperAdmin." />
    <ErrorMessage error={error} />{success && <p className="notice success" role="status">{success}</p>}
    <button className="secondary mb-5" disabled={loading || busy} onClick={() => setRevision(value => value + 1)}>Refresh roles</button>
    {loading ? <p role="status">Loading roles…</p> : roles.length === 0 ? <p className="panel p-6">No roles available. Run the identity seed command.</p> : <div className="space-y-5">{roles.map(role => <form key={`${role.id}-${role.concurrencyStamp}`} className="panel p-6" onSubmit={event => void save(event, role)}>
      <h2 className="text-lg font-semibold">{role.name}</h2>
      {role.isSystemAdmin && <p className="mt-2 text-sm text-slate-500">Full access is fixed for SuperAdmin.</p>}
      <fieldset disabled={role.isSystemAdmin || busy}><legend className="sr-only">{role.name} permissions</legend>
        <div className="my-5 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{permissions.map(permission => <label className="check-label" key={permission}><input type="checkbox" name="permissions" value={permission} defaultChecked={role.permissions.includes(permission)} /><span className="font-mono text-xs">{permission}</span></label>)}</div>
      </fieldset>
      {!role.isSystemAdmin && <button className="secondary" disabled={busy}>{busy ? 'Saving…' : 'Save permissions'}</button>}
    </form>)}</div>}
  </>
}
