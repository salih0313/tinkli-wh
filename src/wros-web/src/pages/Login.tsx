import { useState } from 'react'
import type { FormEvent } from 'react'
import { Navigate } from 'react-router-dom'
import { mutate } from '../api/client'
import { useSession } from '../auth/SessionContext'
import { ErrorMessage, Field } from '../components/Form'

export default function Login() {
  const { user, refresh } = useSession()
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
  return <main className="flex min-h-screen items-center justify-center bg-slate-50 px-5 py-10">
    <div className="panel w-full max-w-md p-8">
      <p className="mb-8 font-bold tracking-widest text-teal-700">WROS</p>
      <h1 className="text-3xl font-semibold">Sign in</h1>
      <p className="mb-6 mt-2 text-sm text-slate-600">Warehouse receiving & inventory</p>
      <form onSubmit={submit} className="space-y-5" aria-busy={busy}>
        <ErrorMessage error={error} />
        <Field label="Username"><input name="username" autoComplete="username" required maxLength={256} autoFocus /></Field>
        <Field label="Password"><input name="password" type="password" autoComplete="current-password" required maxLength={128} /></Field>
        <button className="primary w-full" disabled={busy}>{busy ? 'Signing in…' : 'Sign in'}</button>
      </form>
      <p className="mt-6 text-sm text-slate-500">Accounts are managed by your SuperAdmin. Contact them if you need access or a password reset.</p>
    </div>
  </main>
}
