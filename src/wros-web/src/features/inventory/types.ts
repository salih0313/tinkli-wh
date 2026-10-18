export type InventoryRow = {
  id: string
  productId: string
  sku: string
  product: string
  barcode: string | null
  brand: string | null
  stockLotId: string
  supplierBatch: string | null
  expiry: string | null
  receivedAt: string
  deliveryNoteId: string
  deliveryNoteNumber: string
  warehouseId: string
  warehouse: string
  areaId: string
  rackId: string
  locationId: string | null
  path: string
  quantity: number
  sellableQuantity: number
}
export type ProductSummary = {
  productId: string
  sku: string
  product: string
  physical: number
  sellable: number
}
export type LedgerRow = {
  id: string
  type: string
  stockLotId: string
  sku: string
  product: string
  quantity: number
  path: string
  reference: string
  timestamp: string
  actor: string | null
  integrationClientId?: string | null
  externalOrderId?: string | null
  actorId: string | null
  correlationId: string
  referenceNumber?: string
  referenceType?: string
}
