import { useEffect, useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { mutate } from '../api/client'
import { useSession } from '../auth/SessionContext'
import { ErrorMessage, Field } from '../components/Form'
import { Brand } from '../components/layout/AppShell'
import { Button } from '../components/ui/Controls'
import { Icon } from '../components/ui/Icon'
import { PasswordInput } from '../components/forms/PasswordInput'

export default function Login() {
  const { user, refresh } = useSession()
  useEffect(() => { document.title = 'Sign in · WROS' }, [])
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  if (user) return <Navigate to={user.mustChangePassword ? '/password' : '/'} replace />
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault(); setBusy(true); setError(null)
    const form = event.currentTarget
    const data = new FormData(form)
    try {
      await mutate('POST', '/api/v1/auth/login', { userName: data.get('username'), password: data.get('password') })
      form.reset()
      const session = await refresh()
      if (!session) throw new Error('The browser did not accept the session cookie. Open the app at http://localhost:5173 through the Vite proxy, or use a same-origin HTTPS setup.')
    } catch (failure) { setError(failure) }
    finally { setBusy(false) }
  }
  return <main className="login-page">
    <aside className="login-story"><Brand /><div className="login-story-copy"><p className="eyebrow">Your operations, connected</p><h2>A clear view.<br />A confident team.</h2><p>One workspace for the people behind your warehouse operations.</p></div><div className="warehouse-graphic" aria-hidden="true"><Icon name="warehouse" /></div><p className="login-story-footer"><Icon name="shield" width="16" height="16" />Warehouse Receiving & Operations System</p></aside>
    <section className="login-main" aria-labelledby="login-title"><div className="login-mobile-brand"><Brand /></div><div className="login-form-wrap">
      <span className="login-lock"><Icon name="lock" /></span><p className="eyebrow">Welcome to WROS</p><h1 id="login-title">Sign in to your workspace</h1>
      <p className="login-intro">Enter your account details to get started.</p>
      <form onSubmit={submit} className="space-y-5" aria-busy={busy}>
        <ErrorMessage error={error} />
        <Field label="Username"><input name="username" autoComplete="username" autoCapitalize="none" spellCheck={false} placeholder="Your username" required maxLength={256} /></Field>
        <Field label="Password"><PasswordInput name="password" autoComplete="current-password" placeholder="Enter your password" required /></Field>
        <Button type="submit" className="w-full" loading={busy} icon="arrow">{busy ? 'Signing in…' : 'Sign in'}</Button>
      </form>
      <p className="login-support">Need access or a password reset?<br />Contact your SuperAdmin for help with your account.</p>
    </div><p className="login-footer">WROS · A workspace built for your operations</p></section>
  </main>
}
