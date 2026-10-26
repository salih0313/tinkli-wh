import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { get, mutate } from '../api/client'
import type { Role } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { ErrorMessage, Heading } from '../components/Form'
import { Button, Badge } from '../components/ui/Controls'
import { Icon } from '../components/ui/Icon'
import { EmptyState, LoadingSkeleton, Toast } from '../components/feedback/States'
import { humanize } from '../lib/presentation'

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
  return <><Heading eyebrow="Administration" title="Roles & permissions" detail="Define access once. Keep your team’s responsibilities clear." action={<Button variant="secondary" icon="refresh" disabled={loading || busy} onClick={() => setRevision(value => value + 1)}>Refresh roles</Button>} />
    <ErrorMessage error={error} />{success && <Toast onDismiss={() => setSuccess('')}>{success}</Toast>}
    <div className="notice border-brand-100 bg-brand-50 text-brand-700"><Icon name="shield" /><div>User and role administration always require SuperAdmin. Permissions apply across all warehouses.</div></div>
    {loading ? <div className="panel"><LoadingSkeleton label="Loading roles…" rows={6} /></div> : roles.length === 0 ? <div className="panel"><EmptyState title={error ? 'Roles unavailable' : 'No roles available'} detail={error ? 'Check the connection and use Refresh roles to try again.' : 'Run the identity seed command to create the defined workspace roles.'} icon="shield" /></div> : <div className="space-y-3">{roles.map(role => <details key={`${role.id}-${role.concurrencyStamp}`} className="panel role-card">
      <summary><div><span className="stat-icon"><Icon name={role.isSystemAdmin ? 'shield' : 'users'} /></span><div><h2>{humanize(role.name)}</h2><p>{role.permissions.length} permission{role.permissions.length === 1 ? '' : 's'}{role.isSystemAdmin ? ' · Fixed full access' : ' · Customizable access'}</p></div></div><Icon name="down" width="18" height="18" /></summary>
      <form onSubmit={event => void save(event, role)} aria-busy={busy}>
      {role.isSystemAdmin && <div className="mb-4"><Badge tone="blue">System role</Badge><p className="field-hint mt-2">SuperAdmin has full access. These permissions cannot be changed.</p></div>}
      <fieldset disabled={role.isSystemAdmin || busy}><legend className="sr-only">{role.name} permissions</legend>
        <div className="form-grid mb-5">{permissions.map(permission => <label className="check-label" key={permission}><input type="checkbox" name="permissions" value={permission} defaultChecked={role.permissions.includes(permission)} /><span>{humanize(permission)}<small>{permission}</small></span></label>)}</div>
      </fieldset>
      {!role.isSystemAdmin && <div className="dialog-actions"><Button type="submit" loading={busy}>{busy ? 'Saving…' : 'Save permissions'}</Button></div>}
    </form></details>)}</div>}
  </>
}
