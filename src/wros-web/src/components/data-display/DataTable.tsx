import type { ReactNode } from 'react'
export interface Column<T> { key: string; label: string; render: (item: T) => ReactNode }
export function DataTable<T>({ items, columns, caption, getKey }: { items: T[]; columns: Column<T>[]; caption: string; getKey: (item: T) => string }) {
  return <><div className="desktop-table"><table><caption className="sr-only">{caption}</caption><thead><tr>{columns.map(column => <th key={column.key} scope="col">{column.label}</th>)}</tr></thead><tbody>{items.map(item => <tr key={getKey(item)}>{columns.map(column => <td key={column.key}>{column.render(item)}</td>)}</tr>)}</tbody></table></div>
    <ul className="mobile-records" aria-label={caption}>{items.map(item => <li key={getKey(item)}><dl>{columns.map(column => <div key={column.key}><dt>{column.label}</dt><dd>{column.render(item)}</dd></div>)}</dl></li>)}</ul></>
}
