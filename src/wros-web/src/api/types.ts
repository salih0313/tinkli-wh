export interface Session {
  id: string
  userName: string
  roles: string[]
  permissions: string[]
  mustChangePassword: boolean
  expiresAt: string | null
}
export interface User {
  id: string
  userName: string
  isActive: boolean
  mustChangePassword: boolean
  isLockedOut: boolean
  lockoutEnd: string | null
  roles: string[]
  concurrencyStamp: string
  createdAt: string
}
export interface Role {
  id: string
  name: string
  permissions: string[]
  concurrencyStamp: string
  isSystemAdmin: boolean
}
export interface Page<T> { items: T[]; page: number; pageSize: number; total: number }
export interface AuditEntry {
  id: string; actorId: string | null; timestamp: string; action: string; entity: string
  entityId: string | null; oldValues: string | null; newValues: string | null; correlationId: string
}
