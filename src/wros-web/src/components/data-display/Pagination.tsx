import { Button } from '../ui/Controls'
export function Pagination({ page, total, pageSize = 20, noun, disabled = false, onPage }: { page: number; total: number; pageSize?: number; noun: string; disabled?: boolean; onPage: (value: number) => void }) {
  return <nav className="pagination" aria-label={`${noun} pagination`}><p><strong>{total}</strong> {noun}<span className="pagination-divider">/</span> Page {page} of {Math.max(1, Math.ceil(total / pageSize))}</p><div><Button variant="secondary" disabled={disabled || page <= 1} onClick={() => onPage(page - 1)}>Previous</Button><Button variant="secondary" disabled={disabled || page * pageSize >= total} onClick={() => onPage(page + 1)}>Next</Button></div></nav>
}
