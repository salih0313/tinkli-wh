import { createBrowserRouter, createRoutesFromElements, RouterProvider, Navigate, Outlet, Route } from 'react-router-dom'
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
import DeliveryNotes from './pages/DeliveryNotes'
import DeliveryNoteEditor from './pages/DeliveryNoteEditor'
import DeliveryNoteDetails from './pages/DeliveryNoteDetails'
import ReceiptCorrection from './pages/ReceiptCorrection'
import ReceiptStock from './pages/ReceiptStock'
import Inventory from './pages/Inventory'
import InventoryDashboard from './pages/InventoryDashboard'
import StockMovements from './pages/StockMovements'
import StockTransactions from './pages/StockTransactions'
import Integration from './pages/Integration'
import MasterData from './pages/MasterData'
import PurchaseOrders from './pages/PurchaseOrders'
import PurchaseOrderEditor from './pages/PurchaseOrderEditor'
import PurchaseOrderDetails from './pages/PurchaseOrderDetails'
import { masterReaders } from './features/master-data/types'

function Protected() {
  const { user, loading, error, refresh } = useSession()
  if (loading) return <main className="session-state panel"><LoadingSkeleton label="Checking your session…" /></main>
  if (error) return <main className="mx-auto max-w-xl p-8"><ErrorMessage error={error} /><button className="primary" onClick={() => void refresh().catch(() => {})}>Retry connection</button></main>
  if (!user) return <Navigate to="/login" replace />
  return <AppShell />
}
function Access({ permission, anyOf, admin = false }: { permission?: string; anyOf?: string[]; admin?: boolean }) {
  const { user } = useSession()
  if (user?.mustChangePassword) return <Navigate to="/password" replace />
  if ((admin && !user?.roles.includes('SuperAdmin')) || (permission && !user?.permissions.includes(permission)) || (anyOf && !anyOf.some(value => user?.permissions.includes(value))))
    return <ErrorMessage error="You do not have permission to access this page." />
  return <Outlet />
}
function LoginRoute() {
  const { loading, error, refresh } = useSession()
  if (loading) return <main className="session-state panel"><LoadingSkeleton label="Checking your session…" /></main>
  if (error) return <main className="p-8"><ErrorMessage error={error} /><button className="primary" onClick={() => void refresh().catch(() => {})}>Retry connection</button></main>
  return <Login />
}
const router = createBrowserRouter(createRoutesFromElements(<Route element={<SessionProvider><Outlet /></SessionProvider>}>
    <Route path="/login" element={<LoginRoute />} />
    <Route element={<Protected />}>
      <Route path="/password" element={<Password />} />
      <Route element={<Access />}><Route index element={<Home />} /><Route path="/system" element={<Foundation />} /></Route>
      <Route element={<Access admin permission="users.manage" />}><Route path="/users" element={<Users />} /></Route>
      <Route element={<Access admin permission="roles.manage" />}><Route path="/roles" element={<Roles />} /></Route>
      <Route element={<Access permission="receiving.manage" />}>
        <Route path="/delivery-notes" element={<DeliveryNotes />} />
        <Route path="/delivery-notes/new" element={<DeliveryNoteEditor key="new" />} />
        <Route path="/delivery-notes/:id" element={<DeliveryNoteDetails />} />
        <Route path="/delivery-notes/:id/edit" element={<DeliveryNoteEditor key="edit" />} />
        <Route path="/delivery-notes/:id/correct" element={<ReceiptCorrection />} />
      </Route>
      <Route element={<Access permission="inventory.view" />}><Route path="/receipt-stock" element={<ReceiptStock />} /><Route path="/inventory" element={<Inventory />} /><Route path="/inventory-overview" element={<InventoryDashboard />} /><Route path="/expiry" element={<Inventory expiryOnly />} /><Route path="/stock-movements" element={<StockMovements />} /></Route>
      <Route element={<Access permission="outbound.manage" />}><Route path="/supplier-returns" element={<StockTransactions key="SupplierReturn" kind="SupplierReturn" />} /><Route path="/supplier-returns/:id" element={<StockTransactions key="SupplierReturn" kind="SupplierReturn" />} /></Route>
      <Route element={<Access permission="inventory.adjust" />}><Route path="/disposals" element={<StockTransactions key="Disposal" kind="Disposal" />} /><Route path="/disposals/:id" element={<StockTransactions key="Disposal" kind="Disposal" />} /></Route>
      <Route element={<Access permission="inventory.transfer" />}><Route path="/transfers" element={<StockTransactions key="Transfer" kind="Transfer" />} /><Route path="/transfers/:id" element={<StockTransactions key="Transfer" kind="Transfer" />} /></Route>
      <Route element={<Access permission="purchaseorders.manage" />}>
        <Route path="/purchase-orders" element={<PurchaseOrders />} />
        <Route path="/purchase-orders/new" element={<PurchaseOrderEditor key="new" />} />
        <Route path="/purchase-orders/:id" element={<PurchaseOrderDetails />} />
        <Route path="/purchase-orders/:id/edit" element={<PurchaseOrderEditor key="edit" />} />
      </Route>
      <Route element={<Access anyOf={masterReaders} />}>
        <Route path="/suppliers" element={<MasterData key="suppliers" kind="suppliers" />} />
        <Route path="/products" element={<MasterData key="products" kind="products" />} />
        <Route path="/warehouses" element={<MasterData key="warehouses" kind="warehouses" />} />
        <Route path="/warehouses/:warehouseId/manage" element={<MasterData key="warehouse-manage" kind="warehouses" />} />
        <Route path="/areas" element={<MasterData key="areas" kind="areas" />} />
        <Route path="/racks" element={<MasterData key="racks" kind="racks" />} />
        <Route path="/locations" element={<MasterData key="locations" kind="locations" />} />
        <Route path="/warehouses/:parentId/areas" element={<MasterData key="areas" kind="areas" />} />
        <Route path="/areas/:parentId/racks" element={<MasterData key="racks" kind="racks" />} />
        <Route path="/racks/:parentId/locations" element={<MasterData key="locations" kind="locations" />} />
      </Route>
      <Route element={<Access permission="integration.manage" />}><Route path="/integration" element={<Integration />} /></Route>
      <Route element={<Access permission="audit.view" />}><Route path="/audit" element={<Audit />} /></Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Route>
  </Route>))
export default function App() { return <RouterProvider router={router} /> }
