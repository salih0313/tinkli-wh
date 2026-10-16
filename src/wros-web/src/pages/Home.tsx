import { useSession } from '../auth/SessionContext'
import { Heading } from '../components/Form'
export default function Home() {
  const { user } = useSession()
  return <><Heading title={`Welcome, ${user?.userName}`} detail="Your warehouse workspace is ready for identity and account management." />
    <section className="panel p-6"><h2 className="font-semibold">Your access</h2>
      <div className="mt-4 flex flex-wrap gap-2">{user?.roles.map(role => <span key={role} className="badge">{role}</span>)}</div>
      <p className="mt-5 text-sm text-slate-600">Permissions apply across all warehouses. Warehouse operations arrive in later phases.</p>
      <ul className="mt-4 grid gap-2 text-sm sm:grid-cols-2">{user?.permissions.map(permission => <li key={permission} className="font-mono text-slate-600">{permission}</li>)}</ul>
      {user?.permissions.length === 0 && <p className="mt-4 text-slate-500">No operational permissions assigned. Contact your SuperAdmin.</p>}
    </section></>
}
