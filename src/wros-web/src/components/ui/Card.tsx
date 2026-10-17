import type { HTMLAttributes, ReactNode } from 'react'
export function Card({ className = '', children, ...props }: HTMLAttributes<HTMLElement>) {
  return <section className={`panel ${className}`} {...props}>{children}</section>
}
export function SectionHeader({ title, detail, action }: { title: string; detail?: string; action?: ReactNode }) {
  return <div className="section-header"><div><h2>{title}</h2>{detail && <p>{detail}</p>}</div>{action}</div>
}
