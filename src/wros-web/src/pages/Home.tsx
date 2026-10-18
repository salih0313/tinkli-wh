import { Link } from 'react-router-dom'
import { useSession } from '../auth/SessionContext'
import { Heading } from '../components/Form'
import { Card, SectionHeader } from '../components/ui/Card'
import { Badge } from '../components/ui/Controls'
import { Icon } from '../components/ui/Icon'
import type { IconName } from '../components/ui/Icon'
import { EmptyState } from '../components/feedback/States'
import { humanize } from '../lib/presentation'
export default function Home() {
  const { user } = useSession()
  const admin = user?.roles.includes('SuperAdmin')
  const shortcuts: { to: string; title: string; detail: string; icon: IconName }[] = [
    ...(user?.permissions.includes('receiving.manage') ? [{ to: '/delivery-notes', title: 'Receive a delivery', detail: 'Draft receipts, split lot allocations and post stock', icon: 'warehouse' as const }] : []),
    ...(user?.permissions.includes('purchaseorders.manage') ? [{ to: '/purchase-orders', title: 'Purchase orders', detail: 'Supplier requests, revisions and printable documents', icon: 'audit' as const }] : []),
    ...(user?.permissions.includes('inventory.view') ? [{ to: '/receipt-stock', title: 'Receipt stock', detail: 'Read-only on-hand balances by receipt lot and location', icon: 'overview' as const }] : []),
    ...(admin ? [{ to: '/users', title: 'Manage your team', detail: 'Staff accounts, roles and account access', icon: 'users' as const }, { to: '/roles', title: 'Review permissions', detail: 'Control what each role can do', icon: 'shield' as const }] : []),
    ...(user?.permissions.includes('audit.view') ? [{ to: '/audit', title: 'View security activity', detail: 'Account events and permission changes', icon: 'audit' as const }] : []),
    { to: '/password', title: 'Account & password', detail: 'Review your account and update your password', icon: 'key' },
    { to: '/system', title: 'Check system status', detail: 'API and database connection readiness', icon: 'activity' },
  ]
  return <><Heading eyebrow="Workspace overview" title={`Welcome back, ${user?.userName}`} detail="Your access and warehouse workflow shortcuts. All in one place." action={<Badge tone="blue">Warehouse workspace</Badge>} />
    <div className="stat-grid"><section className="panel stat-card"><div><p>Assigned roles</p><strong>{user?.roles.length ?? 0}</strong><small>Your account memberships</small></div><span className="stat-icon"><Icon name="users" /></span></section><section className="panel stat-card"><div><p>Granted permissions</p><strong>{user?.permissions.length ?? 0}</strong><small>Available to your account</small></div><span className="stat-icon"><Icon name="shield" /></span></section><section className="panel stat-card"><div><p>Warehouse access</p><strong className="!text-xl !leading-[2.1]">All warehouses</strong><small>Shared access across the workspace</small></div><span className="stat-icon"><Icon name="warehouse" /></span></section></div>
    <div className="home-grid"><Card className="overflow-hidden"><SectionHeader title="Your access" detail="Permissions assigned through your account roles" action={<div className="role-tags">{user?.roles.map(role => <Badge key={role} tone="blue">{humanize(role)}</Badge>)}</div>} />
      {user?.permissions.length ? <ul className="permission-list">{user.permissions.map(permission => <li key={permission}><Icon name="check" /><span>{humanize(permission)}<small>{permission}</small></span></li>)}</ul> : <EmptyState title="No permissions assigned" detail="Contact your SuperAdmin to request the access you need." icon="shield" />}
      <p className="context-note">Posted delivery notes create physical stock. Purchase orders and draft receipts do not change inventory. Permissions apply across all warehouses.</p>
    </Card><Card className="overflow-hidden"><SectionHeader title="Workspace essentials" detail="Go straight to what you need" />{shortcuts.map(link => <Link key={link.to} to={link.to} className="quick-link"><span className="stat-icon"><Icon name={link.icon} /></span><span><strong>{link.title}</strong><small>{link.detail}</small></span><Icon name="chevron" /></Link>)}</Card></div>
  </>
}
