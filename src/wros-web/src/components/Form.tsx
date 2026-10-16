import type { ReactNode } from 'react'
import { ApiError } from '../api/client'

export function ErrorMessage({ error }: { error: unknown }) {
  if (!error) return null
  return <div role="alert" className="notice error">{error instanceof Error ? error.message : String(error)}
    {error instanceof ApiError && error.correlationId && <small className="mt-2 block break-all">Reference: {error.correlationId}</small>}</div>
}
export function Field({ label, children }: { label: string; children: ReactNode }) {
  return <label className="field"><span>{label}</span>{children}</label>
}
export function Heading({ title, detail }: { title: string; detail: string }) {
  return <div className="mb-7"><h1 className="text-3xl font-semibold tracking-tight">{title}</h1><p className="mt-2 text-slate-600">{detail}</p></div>
}
export function PasswordHint() {
  return <p id="password-policy" className="text-sm text-slate-500">Use 8–128 characters, including uppercase, lowercase, a number and a symbol; at least four distinct characters.</p>
}
