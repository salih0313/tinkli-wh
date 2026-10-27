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

// Display only business values in history; retain the original payload for API use.
export function displayChangeDetails(value: string): string {
  const uuid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i
  try {
    const visible = JSON.stringify(JSON.parse(value), (key, item: unknown) => {
      if (/^(id|Id|.*Id|.*Ids|rowVersion|concurrencyStamp|createdBy|updatedBy|postedBy)$/.test(key)) return undefined
      if (typeof item === 'string' && uuid.test(item)) return undefined
      return item
    }, 2)
    return visible ?? 'No displayable changes'
  } catch {
    return value.replace(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi, '(internal reference)')
  }
}
