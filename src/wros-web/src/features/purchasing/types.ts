export interface OrderLine { id?: string; productId: string; sku?: string; name?: string; quantity: number }
export interface PurchaseOrder {
  id: string; number: string; supplierId: string; supplierCode: string; supplierName: string;
  orderDate: string; expectedDeliveryDate: string | null; remarks: string | null;
  status: 'Draft' | 'Sent' | 'Locked'; revision: number; rowVersion: string;
  createdAt: string; updatedAt: string; createdBy: string | null; updatedBy: string | null; lines: OrderLine[];
}
export interface OrderRevision { revision: number; action: string; timestamp: string; actorId: string | null; snapshot: string }
