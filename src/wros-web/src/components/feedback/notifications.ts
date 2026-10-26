import type { ReactNode } from 'react'

export interface Notification { id: number; tone: 'success' | 'error'; message: ReactNode; reference?: string; key?: string; onDismiss?: () => void }
let nextId = 0
let notifications: Notification[] = []
const listeners = new Set<() => void>()
function emit() { listeners.forEach(listener => listener()) }
export function subscribeNotifications(listener: () => void) { listeners.add(listener); return () => { listeners.delete(listener) } }
export function getNotifications() { return notifications }
export function dismissNotification(id: number) {
  const item = notifications.find(item => item.id === id)
  notifications = notifications.filter(item => item.id !== id); emit(); item?.onDismiss?.()
}
export function notify(message: ReactNode, tone: Notification['tone'] = 'success', options: Pick<Notification, 'reference' | 'key' | 'onDismiss'> = {}) {
  if (!message) return
  if (options.key && notifications.some(item => item.key === options.key)) return
  notifications = [...notifications, { id: ++nextId, message, tone, ...options }]; emit()
}
export function notifySuccess(message: string) { notify(message) }
