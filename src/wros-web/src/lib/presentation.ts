const permissionLabels: Record<string, string> = {
  'warehouses.manage': 'Manage warehouses & locations',
  'users.manage': 'Manage users',
  'roles.manage': 'Manage roles',
  'suppliers.manage': 'Manage suppliers',
  'products.manage': 'Manage products',
  'purchaseorders.manage': 'Manage purchase orders',
  'receiving.manage': 'Manage receiving',
  'inventory.view': 'View inventory',
  'inventory.adjust': 'Adjust inventory',
  'inventory.transfer': 'Transfer inventory',
  'outbound.manage': 'Manage outbound operations',
  'integration.manage': 'Manage integration',
  'audit.view': 'View audit log',
}
export function humanize(value: string) {
  return permissionLabels[value] ?? value.replace(/([a-z])([A-Z])/g, '$1 $2').replace(/[._]/g, ' ').replace(/^./, letter => letter.toUpperCase())
}
