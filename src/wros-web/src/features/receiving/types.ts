export interface Placement { rackId: string; locationId: string | null; warehouseId: string; path: string }
export interface Allocation { rackId: string; locationId: string | null; quantity: number; path?: string }
export interface ReceiptLot { id: string; supplierBatch: string | null; expiry: string | null; quantity: number; receivedAt: string; allocations: Allocation[]; placements: Allocation[]; balances: Balance[] }
export interface ReceiptLine { id: string; productId: string; sku: string; name: string; trackExpiry: boolean; quantity: number; lots: ReceiptLot[] }
export interface ReceiptSummary { id: string; number: string; supplierName: string; purchaseOrderNumber: string | null; receivedAt: string; status: 'Draft' | 'Posted' }
export interface Receipt extends ReceiptSummary {
  supplierId: string; supplierCode: string; purchaseOrderId: string | null; supplierReference: string | null; remarks: string | null;
  postedAt: string | null; postedBy: string | null; createdAt: string; updatedAt: string; createdBy: string | null; updatedBy: string | null;
  rowVersion: string; lines: ReceiptLine[];
}
export interface Balance { id: string; deliveryNoteId: string; deliveryNoteNumber: string; productId: string; sku: string; product: string; stockLotId: string; supplierBatch: string | null; expiry: string | null; receivedAt: string; warehouseId: string; warehouse: string; rackId: string; locationId: string | null; path: string; quantity: number; sellableQuantity: number }
export interface Movement { id: string; stockLotId: string; warehouseId: string; rackId: string; locationId: string | null; type: string; quantity: number; timestamp: string; actorId: string | null; correlationId: string; correctionId: string | null }
export interface DraftLot { supplierBatch: string; expiry: string; quantity: number; allocations: Allocation[] }
export interface DraftLine { productId: string; quantity: number; lots: DraftLot[] }
export interface ReceiptOrderLookup { id: string; number: string; supplierName: string; status: string }
export function localDateTime(value: string) { const date = new Date(value); return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16) }
export function emptyLot(quantity = 0): DraftLot { return { supplierBatch: '', expiry: '', quantity, allocations: [] } }
export function placementKey(a: Pick<Allocation, 'rackId' | 'locationId'>) { return `${a.rackId}:${a.locationId ?? ''}` }

export function formatUnits(value: number) { return Number.isFinite(value) ? value.toLocaleString() : '—' }
