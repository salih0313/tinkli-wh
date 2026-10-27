import { NavLink } from 'react-router-dom'
export function InventoryNav() {
  return <nav className="inventory-nav" aria-label="Inventory pages">
    <NavLink to="/inventory-overview">Overview</NavLink>
    <NavLink to="/inventory" end>Products & stock</NavLink>
    <NavLink to="/expiry">Expiry alerts</NavLink>
    <NavLink to="/stock-movements">Movements</NavLink>
  </nav>
}
