export interface CatalogProduct {
  id: string; supplierId: string; code: string; productId: string | null; name: string;
  brand: string | null; barcode: string | null; rsp: number | null; supplierPrice: number | null;
  discountType: 'None' | 'Percentage' | 'Fixed'; discountValue: number; netUnitPrice: number | null;
  availableQuantity: number | null; latestImportId: string | null; isActive: boolean; updatedAt: string; rowVersion: string;
}
export interface PricingSnapshot {
  supplierProductId: string | null; supplierProductCode: string; productName: string; brand: string | null;
  barcode: string | null; rsp: number | null; supplierPrice: number | null; discountType: string; discountValue: number;
  netUnitPrice: number | null; quantity: number; grossAmount?: number | null; discountAmount?: number | null; lineTotal?: number | null; lineTotalText?: string | null;
}
export interface PricingSummary { selectedProducts: number; totalQuantity: number; grossAmount: number; discountAmount: number; netAmount: number; unpricedLines: number; grossAmountText: string; discountAmountText: string; netAmountText: string; totalQuantityText: string }
export interface ImportPreview {
  id: string; fileName: string; headers: string[]; mapping: Record<string, number>; rowVersion: string;
  totalRows: number; validRows: number; invalidRows: number; newProducts: number; existingProducts: number; confirmed: boolean;
  rows: { rowNumber: number; product: Omit<CatalogProduct, 'id'> | null; netUnitPrice: number | null; existing: boolean; errors: string[] }[];
}
export interface ImportHistory { id: string; fileName: string; createdAt: string; createdBy: string | null; confirmedAt: string | null; rowCount: number }
export interface Page<T> { items: T[]; page: number; pageSize: number; total: number }
export function priceLabel(value: number | null | undefined) { return value == null ? '—' : value.toLocaleString('en-KW', { minimumFractionDigits: 3, maximumFractionDigits: 3 }) }
export function discountLabel(type?: string, value?: number) { return type === 'Percentage' ? `${value ?? 0}%` : type === 'Fixed' ? `${priceLabel(value)} KWD` : '—' }
// Preview arithmetic uses integer fils. Saved financial totals always come from the backend.
export function fils(value: number) { return BigInt(Math.round(value * 1000)) }
export function formatFils(value: bigint) { const negative = value < 0n; const n = negative ? -value : value; return `${negative ? '-' : ''}${(n / 1000n).toLocaleString('en-US')}.${(n % 1000n).toString().padStart(3, '0')}` }
export function productSnapshot(p: CatalogProduct, quantity: number): PricingSnapshot {
  return { supplierProductId: p.id, supplierProductCode: p.code, productName: p.name, brand: p.brand, barcode: p.barcode, rsp: p.rsp, supplierPrice: p.supplierPrice, discountType: p.discountType, discountValue: p.discountValue, netUnitPrice: p.netUnitPrice, quantity }
}

export function exactMoney(value: string) { const [whole, fraction] = value.split('.'); return `${BigInt(whole).toLocaleString('en-US')}.${fraction}` }
