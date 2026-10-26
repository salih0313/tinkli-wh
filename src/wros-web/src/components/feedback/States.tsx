import type { ReactNode } from 'react'
import { Icon } from '../ui/Icon'
import type { IconName } from '../ui/Icon'
export function EmptyState({ title, detail, icon = 'search', action }: { title: string; detail: string; icon?: IconName; action?: ReactNode }) {
  return <div className="empty-state"><span className="empty-icon"><Icon name={icon} /></span><h2>{title}</h2><p>{detail}</p>{action}</div>
}
export function LoadingSkeleton({ label = 'Loading…', rows = 3 }: { label?: string; rows?: number }) {
  return <div className="loading-state" role="status"><span className="sr-only">{label}</span><div aria-hidden="true">{Array.from({ length: rows }, (_, index) => <div key={index} className="skeleton-row"><span className="skeleton-avatar" /><div><span className="skeleton-line" /><span className="skeleton-line short" /></div></div>)}</div></div>
}
