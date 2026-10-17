import type { ButtonHTMLAttributes, InputHTMLAttributes, ReactNode, SelectHTMLAttributes } from 'react'
import { Icon } from './Icon'
import type { IconName } from './Icon'

export function Button({ variant = 'primary', icon, loading = false, children, className = '', disabled, type = 'button', ...props }: ButtonHTMLAttributes<HTMLButtonElement> & { variant?: 'primary' | 'secondary' | 'ghost' | 'danger'; icon?: IconName; loading?: boolean }) {
  return <button type={type} disabled={disabled || loading} aria-busy={loading || undefined} className={`button ${variant} ${className}`} {...props}>
    {loading ? <span className="spinner" aria-hidden="true" /> : icon && <Icon name={icon} />}{children}
  </button>
}
export function IconButton({ label, icon, ...props }: Omit<Parameters<typeof Button>[0], 'children' | 'icon'> & { label: string; icon: IconName }) {
  return <Button variant="ghost" {...props} icon={icon} aria-label={label} title={label} className={`icon-button ${props.className ?? ''}`} />
}
export function Input(props: InputHTMLAttributes<HTMLInputElement>) { return <input {...props} /> }
export function Select({ children, ...props }: SelectHTMLAttributes<HTMLSelectElement>) { return <select {...props}>{children}</select> }
export function Badge({ children, tone = 'neutral' }: { children: ReactNode; tone?: 'neutral' | 'blue' | 'success' | 'warning' | 'danger' }) {
  return <span className={`status-badge ${tone}`}><span className="status-dot" />{children}</span>
}
