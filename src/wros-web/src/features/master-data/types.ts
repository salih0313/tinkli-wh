export interface MasterRecord {
  id: string; code: string; name: string; description: string | null; isActive: boolean;
  rowVersion: string; parentId: string | null; parentPath: string | null; createdAt: string;
  contactPerson: string | null; phone: string | null; email: string | null; address: string | null; remarks: string | null;
  barcode: string | null; brand: string | null; trackExpiry: boolean; externalProductId: string | null;
}
export const masterReaders = ['suppliers.manage', 'products.manage', 'warehouses.manage', 'purchaseorders.manage', 'receiving.manage', 'inventory.view']
