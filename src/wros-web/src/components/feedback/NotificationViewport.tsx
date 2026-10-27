import { useEffect, useRef, useState, useSyncExternalStore } from 'react'
import { createPortal } from 'react-dom'
import { Icon } from '../ui/Icon'
import { IconButton } from '../ui/Controls'
import { dismissNotification, getNotifications, notify, subscribeNotifications } from './notifications'
import type { Notification } from './notifications'

function NotificationItem({ item }: { item: Notification }) {
  const [paused, setPaused] = useState(false)
  const ref = useRef<HTMLDivElement>(null)
  useEffect(() => { if (ref.current?.parentElement) ref.current.parentElement.scrollTop = ref.current.parentElement.scrollHeight }, [item.id])
  useEffect(() => {
    if (item.tone === 'error' || paused) return
    const timer = window.setTimeout(() => dismissNotification(item.id), 8000)
    return () => window.clearTimeout(timer)
  }, [item.id, item.tone, paused])
  return <div ref={ref} className={`toast notice ${item.tone}`} role={item.tone === 'error' ? 'alert' : 'status'}
    onMouseEnter={() => setPaused(true)} onMouseLeave={() => setPaused(false)}
    onFocusCapture={() => setPaused(true)} onBlurCapture={event => { if (!event.currentTarget.contains(event.relatedTarget)) setPaused(false) }}>
    <Icon name={item.tone === 'error' ? 'alert' : 'check'} /><div>{item.message}</div>
    <IconButton label="Dismiss notification" icon="close" onClick={() => dismissNotification(item.id)} />
  </div>
}
export function NotificationViewport() {
  const items = useSyncExternalStore(subscribeNotifications, getNotifications)
  const [container, setContainer] = useState<Element>(document.body)
  useEffect(() => {
    // Modal dialogs live above the document. Keep notifications in the active modal
    // so they stay visible, clickable and inside its keyboard focus boundary.
    const update = () => { const dialogs = document.querySelectorAll('dialog[open]'); setContainer(dialogs.item(dialogs.length - 1) ?? document.body) }
    update()
    let validationReported = false
    const invalid = (event: Event) => {
      const input = event.target as HTMLInputElement
      if (!input.validationMessage) return
      event.preventDefault()
      if (validationReported) return
      validationReported = true; queueMicrotask(() => { validationReported = false })
      const label = input.getAttribute('aria-label') || input.labels?.[0]?.textContent || 'Field'
      const message = `${label}: ${input.validationMessage}`
      notify(message, 'error', { key: `validation:${message}` })
      input.focus({ preventScroll: true })
    }
    document.addEventListener('invalid', invalid, true)
    const observer = new MutationObserver(update)
    observer.observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ['open'] })
    return () => { observer.disconnect(); document.removeEventListener('invalid', invalid, true) }
  }, [])
  return createPortal(<div className="notification-viewport" aria-label="Notifications">{items.map(item => <NotificationItem item={item} key={item.id} />)}</div>, container)
}
