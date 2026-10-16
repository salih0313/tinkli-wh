import { useState } from 'react'
import { BrowserRouter, Navigate, NavLink, Outlet, Route, Routes } from 'react-router-dom'
import { SessionProvider, useSession } from './auth/SessionContext'
import { mutate } from './api/client'
import { ErrorMessage } from './components/Form'
import Login from './pages/Login'
import Home from './pages/Home'
import Password from './pages/Password'
import Users from './pages/Users'
import Roles from './pages/Roles'
import Audit from './pages/Audit'
import Foundation from './pages/Foundation'

function Protected() {
  const { user, loading, error, refresh } = useSession()
  if (loading) return <p className="p-10" role="status">Checking your session…</p>
  if (error) return <main className="mx-auto max-w-xl p-8"><ErrorMessage error={error} /><button className="primary" onClick={() => void refresh().catch(() => {})}>Retry connection</button></main>
  if (!user) return <Navigate to="/login" replace />
  return <Layout />
}
function Access({ permission, admin = false }: { permission?: string; admin?: boolean }) {
  const { user } = useSession()
  if (user?.mustChangePassword) return <Navigate to="/password" replace />
  if ((admin && !user?.roles.includes('SuperAdmin')) || (permission && !user?.permissions.includes(permission)))
    return <p role="alert" className="notice error">You do not have permission to access this page.</p>
  return <Outlet />
}
function Layout() {
  const { user, clear } = useSession()
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const admin = user?.roles.includes('SuperAdmin')
  async function logout() {
    setBusy(true); setError(null)
    try { await mutate('POST', '/api/v1/auth/logout'); clear() }
    catch (failure) { setError(failure) }
    finally { setBusy(false) }
  }
  return <div className="min-h-screen bg-slate-50 text-slate-900">
    <a href="#main" className="sr-only focus:not-sr-only focus:block focus:p-3">Skip to content</a>
    <header className="border-b border-slate-200 bg-white"><div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-4 px-5 py-5">
      <NavLink to="/" className="flex items-center gap-3"><span className="rounded-lg bg-slate-900 px-3 py-2 font-bold text-white">W</span><span className="font-bold tracking-wide">WROS</span></NavLink>
      <div className="flex flex-wrap items-center gap-4"><span className="max-w-52 truncate text-sm text-slate-600">{user?.userName}</span><button className="secondary" onClick={() => void logout()} disabled={busy}>{busy ? 'Signing out…' : 'Sign out'}</button></div>
    </div></header>
    <div className="mx-auto max-w-6xl px-5 py-7"><nav aria-label="Main navigation" className="mb-8 flex flex-wrap gap-2">
      {!user?.mustChangePassword && <><NavLink className="nav-link" to="/" end>Overview</NavLink>{admin && <><NavLink className="nav-link" to="/users">Users</NavLink><NavLink className="nav-link" to="/roles">Roles</NavLink></>}{user?.permissions.includes('audit.view') && <NavLink className="nav-link" to="/audit">Security audit</NavLink>}<NavLink className="nav-link" to="/system">System status</NavLink></>}
      <NavLink className="nav-link" to="/password">Change password</NavLink>
    </nav><ErrorMessage error={error} /><main id="main"><Outlet /></main></div>
  </div>
}
function LoginRoute() {
  const { loading, error, refresh } = useSession()
  if (loading) return <p role="status" className="p-10">Checking your session…</p>
  if (error) return <main className="p-8"><ErrorMessage error={error} /><button className="primary" onClick={() => void refresh().catch(() => {})}>Retry connection</button></main>
  return <Login />
}
export default function App() {
  return <BrowserRouter><SessionProvider><Routes>
    <Route path="/login" element={<LoginRoute />} />
    <Route element={<Protected />}>
      <Route path="/password" element={<Password />} />
      <Route element={<Access />}><Route index element={<Home />} /><Route path="/system" element={<Foundation />} /></Route>
      <Route element={<Access admin permission="users.manage" />}><Route path="/users" element={<Users />} /></Route>
      <Route element={<Access admin permission="roles.manage" />}><Route path="/roles" element={<Roles />} /></Route>
      <Route element={<Access permission="audit.view" />}><Route path="/audit" element={<Audit />} /></Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Route>
  </Routes></SessionProvider></BrowserRouter>
}
