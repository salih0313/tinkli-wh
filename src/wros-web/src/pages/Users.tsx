import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { get, mutate } from '../api/client'
import type { Page, Role, User } from '../api/types'
import { useSession } from '../auth/SessionContext'
import { ErrorMessage, Field, Heading, PasswordHint } from '../components/Form'

export default function Users() {
  const { user: current, refresh } = useSession()
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
    if (selected !== 'new' && selected.isActive && !values.has('active') && !window.confirm('Deactivate this user and invalidate their sessions?')) return
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
    if (!window.confirm('Reset this user’s password and invalidate their sessions?')) return
    const form = event.currentTarget
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
  return <><Heading title="Users" detail="Create staff accounts, assign roles and manage account access." />
    <ErrorMessage error={error} />{success && <p role="status" className="notice success">{success}</p>}
    <div className="mb-5 flex flex-wrap gap-3">
      <form className="flex flex-1 gap-2" onSubmit={event => { event.preventDefault(); setPage(1); setQuery(search) }}>
        <input className="min-w-0 flex-1" aria-label="Search username" placeholder="Search username" maxLength={64} value={search} onChange={event => setSearch(event.target.value)} />
        <button className="secondary" disabled={loading || busy}>Search</button>
      </form>
      <button className="secondary" disabled={loading || busy} onClick={() => setRevision(value => value + 1)}>Refresh users</button>
      <button className="primary" disabled={busy} onClick={() => { setSelected('new'); setError(null); setSuccess('') }}>Create user</button>
    </div>
    {selected && <section className="panel mb-6 p-6" aria-labelledby="user-form-title">
      <h2 id="user-form-title" className="mb-5 text-xl font-semibold">{selected === 'new' ? 'New staff account' : selected.userName}</h2>
      <form key={selected === 'new' ? 'new' : selected.id} className="space-y-5" onSubmit={save} aria-busy={busy}>
        {selected === 'new' && <><Field label="Username"><input name="username" autoComplete="off" minLength={3} maxLength={64} pattern="[a-zA-Z0-9._@+\-]{3,64}" required autoFocus /></Field>
          <Field label="Temporary password"><input name="password" type="password" autoComplete="new-password" minLength={8} maxLength={128} required aria-describedby="password-policy" /></Field><PasswordHint /></>}
        <fieldset><legend className="mb-2 text-sm font-medium">Roles</legend><div className="flex flex-wrap gap-4">{roles.map(role => <label key={role.id} className="check-label"><input type="checkbox" name="roles" value={role.name} defaultChecked={selected === 'new' ? role.name === 'WarehouseStaff' : selected.roles.includes(role.name)} />{role.name}</label>)}</div></fieldset>
        {selected !== 'new' && <label className="check-label"><input type="checkbox" name="active" defaultChecked={selected.isActive} />Active account</label>}
        <div className="flex gap-3"><button className="primary" disabled={busy}>{busy ? 'Saving…' : 'Save user'}</button><button type="button" className="secondary" disabled={busy} onClick={() => setSelected(null)}>Cancel</button></div>
      </form>
      {selected !== 'new' && selected.id !== current?.id && <form className="mt-6 space-y-4 border-t border-slate-200 pt-6" onSubmit={reset}>
        <Field label="New temporary password"><input name="newPassword" type="password" autoComplete="new-password" minLength={8} maxLength={128} required /></Field>
        <p className="text-sm text-slate-500">Use the password policy shown on Change password. Resetting does not reactivate an inactive account or clear lockout.</p>
        <button className="secondary" disabled={busy}>Reset password</button>
      </form>}
      {selected !== 'new' && selected.isLockedOut && <button type="button" className="secondary mt-5" disabled={busy} onClick={() => void unlock()}>Unlock account</button>}
    </section>}
    <div className="panel overflow-x-auto" aria-busy={loading}>{loading ? <p className="p-6" role="status">Loading users…</p> : data?.items.length === 0 ? <p className="p-6 text-slate-500">No users match your search.</p> : <table>
      <caption className="sr-only">User accounts and assigned roles</caption><thead><tr><th>Username</th><th>Roles</th><th>Status</th><th>Action</th></tr></thead>
      <tbody>{data?.items.map(item => <tr key={item.id}><td className="font-medium">{item.userName}{item.id === current?.id && <span className="ml-2 text-xs text-slate-500">You</span>}</td><td>{item.roles.join(', ')}</td><td>{!item.isActive ? 'Inactive' : item.isLockedOut ? 'Locked out' : item.mustChangePassword ? 'Password change required' : 'Active'}</td><td><button className="text-button" disabled={busy} onClick={() => { setSelected(item); setError(null); setSuccess(''); window.scrollTo({ top: 0, behavior: 'instant' }) }}>Manage<span className="sr-only"> {item.userName}</span></button></td></tr>)}</tbody>
    </table>}</div>
    <div className="mt-4 flex items-center justify-between text-sm"><span>{data?.total ?? 0} users · Page {page}</span><div className="flex gap-2"><button className="secondary" disabled={busy || loading || page <= 1} onClick={() => setPage(value => value - 1)}>Previous</button><button className="secondary" disabled={busy || loading || !data || page * 20 >= data.total} onClick={() => setPage(value => value + 1)}>Next</button></div></div>
  </>
}
