import { cloneElement, useId } from 'react'
import type { ReactElement, ReactNode } from 'react'
import { ApiError } from '../api/client'
import { Icon } from './ui/Icon'

export function ErrorMessage({ error }: { error: unknown }) {
  if (!error) return null
  return <div role="alert" className="notice error"><Icon name="alert" /><div>{error instanceof Error ? error.message : String(error)}
    {error instanceof ApiError && error.correlationId && <small className="mt-2 block break-all">Reference: {error.correlationId}</small>}</div></div>
}
type FieldWidth = 'short' | 'standard' | 'wide'
export function Field({ label, children, hint, error, width }: { label: string; children: ReactElement<{ id?: string; type?: string; maxLength?: number; 'aria-describedby'?: string; 'aria-invalid'?: boolean }>; hint?: string; error?: string; width?: FieldWidth }) {
  const generatedId = useId(); const id = children.props.id ?? generatedId
  // Use input semantics for consistent widths; explicit width handles long identifiers and free text.
  const type = children.props.type
  const inputSizes: Record<string, string> = { date: 'date', 'datetime-local': 'datetime', number: 'number', tel: 'short', file: 'wide' }
  const contentSize = children.type === 'textarea' ? 'wide' : children.props.maxLength && children.props.maxLength <= 100 ? 'short' : 'standard'
  const size = width ?? inputSizes[type ?? ''] ?? contentSize
  return <div className="field" data-field-size={size}><label htmlFor={id}>{label}</label>{cloneElement(children, { id, 'aria-describedby': [children.props['aria-describedby'], hint ? `${id}-hint` : '', error ? `${id}-error` : ''].filter(Boolean).join(' ') || undefined, 'aria-invalid': error ? true : children.props['aria-invalid'] })}{hint && <span id={`${id}-hint`} className="field-hint">{hint}</span>}{error && <span id={`${id}-error`} className="field-error" role="alert">{error}</span>}</div>
}
export function Heading({ title, detail, action, eyebrow = 'Workspace' }: { title: string; detail: string; action?: ReactNode; eyebrow?: string }) {
  return <div className="page-heading"><div><p className="eyebrow">{eyebrow}</p><h1>{title}</h1><p className="page-description">{detail}</p></div>{action && <div className="heading-action">{action}</div>}</div>
}
export function PasswordHint() {
  return <p id="password-policy" className="field-hint flex items-center gap-2"><Icon name="info" width="16" height="16" />Use at least 8 characters.</p>
}
