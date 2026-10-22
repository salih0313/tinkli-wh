import { useEffect, useRef, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { useSession } from '../../auth/SessionContext'
import { mutate } from '../../api/client'
import { ErrorMessage } from '../Form'
import { Button, IconButton } from '../ui/Controls'
import { Icon } from '../ui/Icon'
import type { IconName } from '../ui/Icon'
import { humanize } from '../../lib/presentation'
import { masterReaders } from '../../features/master-data/types'
import { useBodyScrollLock } from '../../hooks/useBodyScrollLock'

export function Brand({ compact = false }: { compact?: boolean }) {
  return <span className="brand"><img className={`brand-logo${compact ? ' compact' : ''}`} src={compact ? '/branding/logo/logo_charactor.png' : '/branding/logo/logo_full.png'} alt={compact ? 'Tinkli' : 'Tinkli Kids Planet'} />{!compact && <span className="brand-caption">Warehouse operations · WROS</span>}</span>
}
const pageNames: Record<string, string> = { '/inventory-overview':'Inventory overview','/inventory':'Inventory explorer','/expiry':'Expiry alerts','/stock-movements':'Stock movements','/supplier-returns':'Supplier returns','/disposals':'Damage & disposal','/transfers':'Warehouse transfers','/integration':'E-commerce integration', '/delivery-notes': 'Delivery notes', '/receipt-stock': 'Receipt stock', '/purchase-orders': 'Purchase orders', '/suppliers': 'Suppliers', '/products': 'Products', '/warehouses': 'Warehouses', '/areas': 'Warehouse areas', '/racks': 'Storage racks', '/locations': 'Locations', '/': 'Overview', '/users': 'Users', '/roles': 'Roles & permissions', '/audit': 'Security audit', '/password': 'Account & password', '/system': 'System status' }
export default function AppShell() {
  const { user, clear } = useSession()
  const location = useLocation()
  const [collapsed, setCollapsed] = useState(false)
  const [mobileOpen, setMobileOpen] = useState(false)
  const [error, setError] = useState<unknown>(null)
  const [busy, setBusy] = useState(false)
  const drawer = useRef<HTMLDialogElement>(null)
  const menuButton = useRef<HTMLButtonElement>(null)
  const userMenu = useRef<HTMLDetailsElement>(null)
  const admin = user?.roles.includes('SuperAdmin')
  const title = pageNames[location.pathname] ?? (location.pathname.startsWith('/purchase-orders/') ? 'Purchase orders' : location.pathname.startsWith('/delivery-notes/') ? 'Delivery notes' : pageNames['/' + location.pathname.split('/')[1]] ?? pageNames['/' + location.pathname.split('/').at(-1)]) ?? 'Workspace'
  useBodyScrollLock(mobileOpen)
  useEffect(() => {
    setMobileOpen(false)
    if (userMenu.current) userMenu.current.open = false
    document.title = `${title} · WROS`
    window.scrollTo({ top: 0, behavior: 'instant' })
  }, [location.pathname, title])
  useEffect(() => {
    if (!mobileOpen || !drawer.current) return
    drawer.current.showModal()
    const desktop = window.matchMedia('(min-width: 1024px)')
    const resized = () => { if (desktop.matches) setMobileOpen(false) }
    desktop.addEventListener('change', resized)
    return () => { drawer.current?.close(); desktop.removeEventListener('change', resized); menuButton.current?.focus() }
  }, [mobileOpen])
  useEffect(() => {
    function close(event: Event) {
      if (event instanceof KeyboardEvent && event.key === 'Escape' && userMenu.current?.open) {
        userMenu.current.open = false; userMenu.current.querySelector('summary')?.focus()
      } else if (event.type === 'pointerdown' && !userMenu.current?.contains(event.target as Node) && userMenu.current) userMenu.current.open = false
    }
    document.addEventListener('keydown', close); document.addEventListener('pointerdown', close)
    return () => { document.removeEventListener('keydown', close); document.removeEventListener('pointerdown', close) }
  }, [])
  async function logout() {
    setBusy(true); setError(null)
    try { await mutate('POST', '/api/v1/auth/logout'); clear() }
    catch (failure) { setError(failure) }
    finally { setBusy(false) }
  }
  const links: { to: string; label: string; icon: IconName; group: string }[] = [
    ...(!user?.mustChangePassword ? [
      { to: '/', label: 'Overview', icon: 'overview' as const, group: 'Workspace' },
      ...(user?.permissions.includes('purchaseorders.manage') ? [{ to: '/purchase-orders', label: 'Purchase orders', icon: 'audit' as const, group: 'Operations' }] : []),
      ...(user?.permissions.includes('receiving.manage') ? [{ to: '/delivery-notes', label: 'Delivery notes', icon: 'warehouse' as const, group: 'Operations' }] : []),
      ...(user?.permissions.includes('inventory.view') ? [{ to: '/inventory-overview', label: 'Inventory overview', icon: 'overview' as const, group: 'Inventory' }, { to: '/inventory', label: 'Inventory explorer', icon: 'warehouse' as const, group: 'Inventory' }, { to: '/expiry', label: 'Expiry alerts', icon: 'alert' as const, group: 'Inventory' }, { to: '/stock-movements', label: 'Stock movements', icon: 'activity' as const, group: 'Inventory' }] : []),
      ...([['outbound.manage','/supplier-returns','Supplier returns'],['inventory.adjust','/disposals','Damage & disposal'],['inventory.transfer','/transfers','Warehouse transfers']].filter(([permission])=>user?.permissions.includes(permission)).map(([,to,label])=>({to,label,icon:'warehouse' as const,group:'Operations'}))),
      ...(masterReaders.some(permission => user?.permissions.includes(permission)) ? [
        { to: '/suppliers', label: 'Suppliers', icon: 'users' as const, group: 'Master data' },
        { to: '/products', label: 'Products', icon: 'overview' as const, group: 'Master data' },
        { to: '/warehouses', label: 'Warehouses', icon: 'warehouse' as const, group: 'Master data' },
      ] : []),
      ...(admin ? [{ to: '/users', label: 'Users', icon: 'users' as const, group: 'Administration' }, { to: '/roles', label: 'Roles & permissions', icon: 'shield' as const, group: 'Administration' }] : []),
      ...(user?.permissions.includes('integration.manage') ? [{to:'/integration',label:'E-commerce integration',icon:'external' as const,group:'Administration'}] : []),
      ...(user?.permissions.includes('audit.view') ? [{ to: '/audit', label: 'Security audit', icon: 'audit' as const, group: 'Administration' }] : []),
      { to: '/system', label: 'System status', icon: 'activity' as const, group: 'Settings' },
    ] : []),
    { to: '/password', label: 'Account & password', icon: 'key', group: 'Settings' },
  ]
  function navigation(compact = false) {
    return <><div className="sidebar-brand"><NavLink to="/" aria-label="WROS overview"><Brand compact={compact} /></NavLink></div>
      <nav aria-label="Main navigation" className="sidebar-nav">{links.map((link, index) => <div key={link.to}>
        {links[index - 1]?.group !== link.group && <p className={`nav-group ${compact ? 'compact-group' : ''}`}>{compact ? <span className="sr-only">{link.group}</span> : link.group}</p>}
        <NavLink to={link.to} end={link.to === '/'} className="nav-link" title={compact ? link.label : undefined} aria-label={compact ? link.label : undefined} onClick={() => setMobileOpen(false)}><Icon name={link.icon} />{!compact && <span>{link.label}</span>}</NavLink>
      </div>)}</nav>
      <div className="sidebar-foot">{!compact && <><span className="sidebar-workspace"><Icon name="warehouse" />Shared workspace</span><p>All warehouses · v1</p></>}<Button variant="ghost" icon="logout" loading={busy} onClick={() => void logout()} aria-label={compact ? 'Sign out' : undefined} title={compact ? 'Sign out' : undefined}>{!compact && 'Sign out'}</Button></div></>
  }
  return <div className={`app-shell ${collapsed ? 'sidebar-collapsed' : ''}`}>
    <a href="#main" className="skip-link">Skip to content</a>
    <aside className="desktop-sidebar">{navigation(collapsed)}<IconButton label={collapsed ? 'Expand sidebar' : 'Collapse sidebar'} icon="collapse" className="collapse-button" onClick={() => setCollapsed(value => !value)} aria-expanded={!collapsed} /></aside>
    <dialog ref={drawer} className="nav-drawer" aria-label="Main navigation" onCancel={event => { event.preventDefault(); setMobileOpen(false) }}>
      <IconButton label="Close navigation" icon="close" className="drawer-close" onClick={() => setMobileOpen(false)} />{navigation()}
    </dialog>
    <div className="app-workspace"><header className="topbar"><div className="topbar-title"><button ref={menuButton} className="button ghost icon-button mobile-menu-button" aria-label="Open navigation" aria-expanded={mobileOpen} onClick={() => setMobileOpen(true)}><Icon name="menu" /></button><span className="breadcrumb-root">Workspace<Icon name="chevron" width="14" height="14" /></span><span>{title}</span></div>
      <details ref={userMenu} className="user-menu"><summary aria-label={`Account menu for ${user?.userName}`}><span className="avatar">{user?.userName.slice(0, 2).toUpperCase()}</span><span className="user-menu-name"><strong>{user?.userName}</strong><small>{humanize(user?.roles[0] ?? 'Staff')}</small></span><Icon name="down" width="16" height="16" /></summary><div className="user-menu-panel"><p>Signed in as<strong>{user?.userName}</strong></p><NavLink to="/password"><Icon name="key" />Account & password</NavLink><Button variant="ghost" icon="logout" loading={busy} onClick={() => void logout()}>Sign out</Button></div></details>
    </header><main id="main" className="page-content" tabIndex={-1}><ErrorMessage error={error} /><Outlet /></main><footer className="workspace-footer"><span>WROS · Warehouse Receiving & Operations</span><span>Warehouse operations workspace</span></footer></div>
  </div>
}
