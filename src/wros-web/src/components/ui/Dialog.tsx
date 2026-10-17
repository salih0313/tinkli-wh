import { useEffect, useId, useRef } from 'react'
import type { ReactNode } from 'react'
import { IconButton } from './Controls'
import { useBodyScrollLock } from '../../hooks/useBodyScrollLock'

export function Dialog({ open, onClose, title, description, children, busy = false, className = '' }: { open: boolean; onClose: () => void; title: string; description?: string; children: ReactNode; busy?: boolean; className?: string }) {
  const ref = useRef<HTMLDialogElement>(null)
  const titleId = useId(); const detailId = useId()
  useBodyScrollLock(open)
  useEffect(() => {
    const dialog = ref.current
    if (!open || !dialog) return
    const previous = document.activeElement as HTMLElement | null
    dialog.showModal()
    return () => { dialog.close(); previous?.focus() }
  }, [open])
  return <dialog ref={ref} className={`dialog ${className}`} aria-labelledby={titleId} aria-describedby={description ? detailId : undefined}
    onKeyDown={event => {
      if (event.key !== 'Escape') return
      // Handle Escape once so closing a confirmation does not also close its parent dialog.
      event.preventDefault(); event.stopPropagation()
      if (!busy) onClose()
    }}
    onCancel={event => { event.preventDefault(); event.stopPropagation(); if (!busy) window.setTimeout(onClose, 0) }}>
    <div className="dialog-heading"><div><h2 id={titleId}>{title}</h2>{description && <p id={detailId}>{description}</p>}</div><IconButton icon="close" label="Close dialog" disabled={busy} onClick={onClose} /></div>
    <div className="dialog-body">{children}</div>
  </dialog>
}
