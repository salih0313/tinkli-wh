import { notifySuccess } from '../components/feedback/notifications'
import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { mutate } from '../api/client'
import { useSession } from '../auth/SessionContext'
import { ErrorMessage, Field, Heading, PasswordHint } from '../components/Form'
import { Button, Badge } from '../components/ui/Controls'
import { PasswordInput } from '../components/forms/PasswordInput'
import { humanize } from '../lib/presentation'

export default function Password() {
  const { user, clear } = useSession()
  const navigate = useNavigate()
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setError(null)
    const form = event.currentTarget
    const data = new FormData(form)
    if (data.get('newPassword') !== data.get('confirmation')) { setError('New passwords do not match.'); return }
    setBusy(true)
    try {
      await mutate('POST', '/api/v1/auth/change-password', { currentPassword: data.get('currentPassword'), newPassword: data.get('newPassword') })
      notifySuccess('Password changed. Sign in with your new password.'); form.reset(); clear(); navigate('/login', { replace: true })
    } catch (failure) { setError(failure) }
    finally { setBusy(false) }
  }
  return <><Heading eyebrow="Account settings" title="Account & password" detail={user?.mustChangePassword ? 'Replace your temporary password to continue.' : 'Changing your password signs you out on all devices. Sign in again with your new password.'} />
    <div className="account-grid"><aside className="panel account-summary"><span className="avatar">{user?.userName.slice(0, 2).toUpperCase()}</span><h2>{user?.userName}</h2><p className="mt-2">Your workspace account</p><div className="role-tags my-5">{user?.roles.map(role => <Badge key={role} tone="blue">{humanize(role)}</Badge>)}</div><p>Contact your SuperAdmin to update account access or assigned roles.</p></aside><form className="panel account-form space-y-3" onSubmit={submit} aria-busy={busy}>
      <ErrorMessage error={error} />
      <Field label="Current password"><PasswordInput name="currentPassword" autoComplete="current-password" required /></Field>
      <Field label="New password"><PasswordInput name="newPassword" autoComplete="new-password" minLength={8} aria-describedby="password-policy" required /></Field>
      <Field label="Confirm new password"><PasswordInput name="confirmation" autoComplete="new-password" minLength={8} required /></Field>
      <PasswordHint /><Button type="submit" loading={busy} icon="key">{busy ? 'Saving…' : 'Change password and sign out'}</Button>
    </form></div>
  </>
}
