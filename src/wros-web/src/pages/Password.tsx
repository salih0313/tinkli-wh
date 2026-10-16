import { useState } from 'react'
import type { FormEvent } from 'react'
import { useNavigate } from 'react-router-dom'
import { mutate } from '../api/client'
import { useSession } from '../auth/SessionContext'
import { ErrorMessage, Field, Heading, PasswordHint } from '../components/Form'

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
      form.reset(); clear(); navigate('/login', { replace: true })
    } catch (failure) { setError(failure) }
    finally { setBusy(false) }
  }
  return <div className="max-w-xl"><Heading title="Change password" detail={user?.mustChangePassword ? 'Replace your temporary password to continue.' : 'Changing your password signs you out on all devices. Sign in again with your new password.'} />
    <form className="panel space-y-5 p-6" onSubmit={submit} aria-busy={busy}>
      <ErrorMessage error={error} />
      <Field label="Current password"><input name="currentPassword" type="password" autoComplete="current-password" maxLength={128} required /></Field>
      <Field label="New password"><input name="newPassword" type="password" autoComplete="new-password" minLength={8} maxLength={128} aria-describedby="password-policy" required /></Field>
      <Field label="Confirm new password"><input name="confirmation" type="password" autoComplete="new-password" minLength={8} maxLength={128} required /></Field>
      <PasswordHint /><button className="primary" disabled={busy}>{busy ? 'Saving…' : 'Change password and sign out'}</button>
    </form>
  </div>
}
