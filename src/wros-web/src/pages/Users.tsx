import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { get, mutate } from '../api/client'
import type { Page, Role, User } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { ErrorMessage, Field, Heading, PasswordHint } from '../components/Form'
import { Button, Badge } from '../components/ui/Controls'
import { Icon } from '../components/ui/Icon'
import { Dialog } from '../components/ui/Dialog'
import { PasswordInput } from '../components/forms/PasswordInput'
import { DataTable } from '../components/data-display/DataTable'
import { Pagination } from '../components/data-display/Pagination'
import { EmptyState, LoadingSkeleton, Toast } from '../components/feedback/States'
import { useConfirm } from '../hooks/useConfirm'
import { humanize } from '../lib/presentation'

export default function Users() {
  const { user: current, refresh } = useSession()
  const { confirm, confirmation } = useConfirm()
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [query, setQuery] = useState('')
  const [revision, setRevision] = useState(0)
  const [data, setData] = useState<Page<User> | null>(null)
  const [roles, setRoles] = useState<Role[]>([])
  const [selected, setSelected] = useState<User | 'new' | null>(null)
  const [error, setError] = useState<unknown>(null)
  const [success, setSuccess] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    const abort = new AbortController(); setLoading(true); setError(null)
    Promise.all([get<Page<User>>(`/api/v1/users?page=${page}&pageSize=20&search=${encodeURIComponent(query)}`, abort.signal), get<Role[]>('/api/v1/roles', abort.signal)])
      .then(([users, available]) => { setData(users); setRoles(available) })
      .catch(failure => { if (!abort.signal.aborted) setError(failure) })
      .finally(() => { if (!abort.signal.aborted) setLoading(false) })
    return () => abort.abort()
  }, [page, query, revision])
  function reload() { setSelected(null); setRevision(value => value + 1) }
  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selected) return
    const form = event.currentTarget
    const values = new FormData(form)
    const assigned = values.getAll('roles') as string[]
    if (assigned.length === 0) { setError('Choose at least one role.'); return }
    if (selected !== 'new' && selected.isActive && !values.has('active') && !await confirm('Deactivate this user and invalidate their sessions?')) return
    setBusy(true); setError(null); setSuccess('')
    try {
      if (selected === 'new') {
        await mutate('POST', '/api/v1/users', { userName: values.get('username'), password: values.get('password'), roles: assigned })
        setSuccess('User created. Share the temporary password through an approved secure channel. They must change it at first sign-in.')
      } else {
        await mutate('PUT', `/api/v1/users/${selected.id}`, { isActive: values.has('active'), roles: assigned, concurrencyStamp: selected.concurrencyStamp })
        setSuccess('User updated. Their existing sessions are invalidated.')
      }
      form.reset(); reload(); await refresh()
    } catch (failure) { setError(failure) }
    finally { setBusy(false) }
  }
  async function reset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); if (!selected || selected === 'new') return
    const form = event.currentTarget
    if (!await confirm('Reset this user’s password and invalidate their sessions?')) return
    const values = new FormData(form)
    setBusy(true); setError(null); setSuccess('')
    try {
      await mutate('POST', `/api/v1/users/${selected.id}/reset-password`, { newPassword: values.get('newPassword'), concurrencyStamp: selected.concurrencyStamp })
      form.reset(); setSuccess('Password reset. The user must change the temporary password at next sign-in.'); reload()
    } catch (failure) { setError(failure) }
    finally { setBusy(false) }
  }
  async function unlock() {
    if (!selected || selected === 'new') return
    setBusy(true); setError(null)
    try { await mutate('POST', `/api/v1/users/${selected.id}/unlock`, { concurrencyStamp: selected.concurrencyStamp }); setSuccess('Account unlocked.'); reload() }
    catch (failure) { setError(failure) }
    finally { setBusy(false) }
  }
  function manage(item: User) { setSelected(item); setError(null); setSuccess('') }
  return <><Heading eyebrow="Administration" title="Users" detail="The right people. The right access. Manage your workspace team." action={<Button icon="plus" disabled={busy} onClick={() => { setSelected('new'); setError(null); setSuccess('') }}>Create user</Button>} />
    {!selected && <ErrorMessage error={error} />}{success && <Toast onDismiss={() => setSuccess('')}>{success}</Toast>}
    <section className="panel" aria-label="User directory"><div className="toolbar">
      <form className="search-form" onSubmit={event => { event.preventDefault(); setPage(1); setQuery(search) }}>
        <div className="search-control"><Icon name="search" /><input aria-label="Search username" placeholder="Search by username…" maxLength={64} value={search} onChange={event => setSearch(event.target.value)} /></div>
        <Button type="submit" variant="secondary" disabled={loading || busy}>Search</Button>
      </form><Button variant="ghost" icon="refresh" disabled={loading || busy} onClick={() => setRevision(value => value + 1)}>Refresh</Button><span className="table-meta">{data?.total ?? '—'} team members</span>
    </div>
    <div aria-busy={loading}>{loading ? <LoadingSkeleton label="Loading users…" /> : data?.items.length === 0 ? <EmptyState title={query ? 'No matching users' : 'Your team starts here'} detail={query ? 'Try another username or clear your search.' : 'Create a staff account to give your team access.'} icon="users" /> : data && <DataTable items={data.items} getKey={item => item.id} caption="User accounts and assigned roles" columns={[
      { key: 'name', label: 'Team member', render: item => <div className="person-cell"><span className="avatar">{item.userName.slice(0, 2).toUpperCase()}</span><div><strong>{item.userName}</strong>{item.id === current?.id && <small>Your account</small>}</div></div> },
      { key: 'roles', label: 'Roles', render: item => <div className="role-tags">{item.roles.map(role => <Badge key={role}>{humanize(role)}</Badge>)}</div> },
      { key: 'status', label: 'Status', render: item => <Badge tone={!item.isActive ? 'neutral' : item.isLockedOut ? 'danger' : item.mustChangePassword ? 'warning' : 'success'}>{!item.isActive ? 'Inactive' : item.isLockedOut ? 'Locked out' : item.mustChangePassword ? 'Password change required' : 'Active'}</Badge> },
      { key: 'action', label: 'Actions', render: item => <button className="text-button" disabled={busy} onClick={() => manage(item)}>Manage<Icon name="chevron" width="14" height="14" /><span className="sr-only"> {item.userName}</span></button> },
    ]} />}{!loading && !data && !!error && <EmptyState title="User directory unavailable" detail="Check the connection and use Refresh to try again." icon="alert" />}</div></section>
    <Pagination page={page} total={data?.total ?? 0} noun="users" disabled={busy || loading} onPage={setPage} />
    <Dialog open={!!selected} title={selected === 'new' ? 'Create staff account' : selected?.userName ?? 'Manage user'} description={selected === 'new' ? 'Give a team member secure access to the workspace.' : 'Update account access, assigned roles and security.'} busy={busy} onClose={() => { setSelected(null); setError(null) }}>
      <ErrorMessage error={error} />
      {selected && <><form key={selected === 'new' ? 'new' : selected.id} className="space-y-5" onSubmit={save} aria-busy={busy}>
        {selected === 'new' && <><Field label="Username" hint="3–64 characters. Letters, numbers and . _ @ + - are allowed."><input name="username" autoComplete="off" autoCapitalize="none" spellCheck={false} minLength={3} maxLength={64} pattern="[a-zA-Z0-9._@+\-]{3,64}" required autoFocus /></Field>
          <Field label="Temporary password"><PasswordInput name="password" autoComplete="new-password" minLength={8} required aria-describedby="password-policy" /></Field><PasswordHint /></>}
        <fieldset><legend className="form-section-title">Assigned roles</legend><div className="form-grid">{roles.map(role => <label key={role.id} className="check-label"><input type="checkbox" name="roles" value={role.name} defaultChecked={selected === 'new' ? role.name === 'WarehouseStaff' : selected.roles.includes(role.name)} />{humanize(role.name)}</label>)}</div></fieldset>
        {selected !== 'new' && <label className="check-label"><input type="checkbox" name="active" defaultChecked={selected.isActive} />Active account</label>}
        <div className="dialog-actions"><Button variant="secondary" disabled={busy} onClick={() => setSelected(null)}>Cancel</Button><Button type="submit" loading={busy}>{busy ? 'Saving…' : selected === 'new' ? 'Create account' : 'Save changes'}</Button></div>
      </form>
      {selected !== 'new' && selected.id !== current?.id && <form className="security-section space-y-4" onSubmit={reset} aria-busy={busy}><div><h3>Reset password</h3><p className="field-hint">The user will need to change this temporary password at their next sign-in. Existing sessions will end.</p></div>
        <Field label="New temporary password"><PasswordInput name="newPassword" autoComplete="new-password" minLength={8} required /></Field>
        <p className="field-hint">Use at least 8 characters. Resetting does not reactivate an account or clear lockout.</p><Button variant="danger" type="submit" disabled={busy} icon="key">Reset password</Button>
      </form>}
      {selected !== 'new' && selected.isLockedOut && <div className="security-section"><h3>Account lockout</h3><p className="field-hint mb-3">Restore sign-in access after repeated unsuccessful attempts.</p><Button variant="secondary" disabled={busy} onClick={() => void unlock()}>Unlock account</Button></div>}</>}
    </Dialog>{confirmation}
  </>
}
