import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { SessionProvider, useSession } from './auth/SessionContext'
import { ErrorMessage } from './components/Form'
import AppShell from './components/layout/AppShell'
import { LoadingSkeleton } from './components/feedback/States'
import Login from './pages/Login'
import Home from './pages/Home'
import Password from './pages/Password'
import Users from './pages/Users'
import Roles from './pages/Roles'
import Audit from './pages/Audit'
import Foundation from './pages/Foundation'

function Protected() {
  const { user, loading, error, refresh } = useSession()
  if (loading) return <main className="session-state panel"><LoadingSkeleton label="Checking your session…" /></main>
  if (error) return <main className="mx-auto max-w-xl p-8"><ErrorMessage error={error} /><button className="primary" onClick={() => void refresh().catch(() => {})}>Retry connection</button></main>
  if (!user) return <Navigate to="/login" replace />
  return <AppShell />
}
function Access({ permission, admin = false }: { permission?: string; admin?: boolean }) {
  const { user } = useSession()
  if (user?.mustChangePassword) return <Navigate to="/password" replace />
  if ((admin && !user?.roles.includes('SuperAdmin')) || (permission && !user?.permissions.includes(permission)))
    return <p role="alert" className="notice error">You do not have permission to access this page.</p>
  return <Outlet />
}
function LoginRoute() {
  const { loading, error, refresh } = useSession()
  if (loading) return <main className="session-state panel"><LoadingSkeleton label="Checking your session…" /></main>
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
