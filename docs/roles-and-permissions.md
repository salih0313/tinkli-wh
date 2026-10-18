# Roles and permissions

Seeded roles: SuperAdmin, WarehouseManager, WarehouseStaff, PurchasingStaff, InventoryController, Viewer. Role definitions are stable/idempotent. Existing organization mappings are preserved; the initial WarehouseStaff demo has inventory.view only. Do not infer role grants from role names. Use SuperAdmin Roles & permissions to configure actual staff access. All authorized users have access to all warehouses in v1.

| Permission | Enforced operation |
|---|---|
| users.manage | User create/update/activate/deactivate/reset/unlock; additionally SuperAdmin required |
| roles.manage | Role permission changes; additionally SuperAdmin required |
| suppliers.manage | Supplier maintenance |
| products.manage | Product maintenance |
| warehouses.manage | Warehouse/area/rack/location maintenance |
| purchaseorders.manage | PO create/edit/send/revision/print |
| receiving.manage | DN draft/post/correction, receiving lookups, PO read for copying |
| inventory.view | Inventory dashboard/explorer/recommendations/expiry/ledger; legacy receipt-stock read |
| outbound.manage | Completed supplier returns |
| inventory.adjust | Completed damage/disposal; no arbitrary stock-entry adjustment |
| inventory.transfer | Completed interwarehouse transfers |
| integration.manage | Machine client registry activation, stable mappings, integration status/history |
| audit.view | Audit viewer |

No new role grants or permission names were introduced in Phases 5–8. SuperAdmin has full staff access. It does **not** bypass machine assertion/scopes to call machine endpoints. Staff administration still derives actor ID from validated cookie claims. Backend permission handlers read live database grants; navigation is a usability layer, not the security boundary. Refresh frontend session to update displayed menus after grant changes; server enforcement is immediate.

Master data read accepts the existing supplier/product/warehouse/purchasing/receiving/inventory readers; write remains individually permission-protected. Stock operation source/placement/supplier lookup endpoints accept any of outbound.manage/inventory.adjust/inventory.transfer to make the corresponding forms usable; history/post endpoints additionally enforce the exact operation permission centrally mapped in PermissionCatalog. These lookups expose needed stock and non-contact supplier labels, not user administration or supplier contact details.

Machine scopes are separate from role permissions: inventory.snapshot for snapshot capture/pages; orders.deduct for Ready to Deliver. They are assigned in secure configuration, not from submitted JWT scope strings. Active client registry + allowed public key + valid single-use assertion + matching scope are all required. No machine staff-role escalation or warehouse-specific restrictions are implemented.
