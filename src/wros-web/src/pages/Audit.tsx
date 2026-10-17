import { useEffect, useState } from 'react'
import { get } from '../api/client'
import type { AuditEntry, Page } from '../api/types'
import { ErrorMessage, Heading } from '../components/Form'
import { Button, Badge } from '../components/ui/Controls'
import { EmptyState, LoadingSkeleton } from '../components/feedback/States'
import { Pagination } from '../components/data-display/Pagination'
import { humanize } from '../lib/presentation'
export default function Audit() {
  const [page, setPage] = useState(1)
  const [revision, setRevision] = useState(0)
  const [data, setData] = useState<Page<AuditEntry> | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<unknown>(null)
  useEffect(() => {
    const abort = new AbortController(); setLoading(true); setError(null)
    get<Page<AuditEntry>>(`/api/v1/audit?page=${page}&pageSize=20`, abort.signal).then(setData)
      .catch(failure => { if (!abort.signal.aborted) setError(failure) })
      .finally(() => { if (!abort.signal.aborted) setLoading(false) })
    return () => abort.abort()
  }, [page, revision])
  return <><Heading eyebrow="Administration" title="Security audit" detail="A clear record of account, authentication and permission activity. All times are UTC." action={<Button variant="secondary" icon="refresh" disabled={loading} onClick={() => setRevision(value => value + 1)}>Refresh audit</Button>} />
    <ErrorMessage error={error} />
    {loading ? <div className="panel"><LoadingSkeleton label="Loading audit…" /></div> : data?.items.length === 0 ? <div className="panel"><EmptyState title="No activity yet" detail="Account and security events will appear here as your team uses the workspace." icon="audit" /></div> : <div className="space-y-4">{data?.items.map(item => <article className="panel audit-card" key={item.id}>
      <div className="flex flex-wrap justify-between items-center gap-3"><div className="flex flex-wrap items-center gap-3"><h2>{humanize(item.action)}</h2><Badge>{item.entity}</Badge></div><time dateTime={item.timestamp}>{new Date(item.timestamp).toISOString().replace('T', ' ').replace('Z', ' UTC')}</time></div>
      <dl><div><dt>Actor</dt><dd className="font-mono">{item.actorId ?? 'Anonymous / system'}</dd></div><div><dt>Entity reference</dt><dd>{item.entity} {item.entityId}</dd></div><div className="sm:col-span-2"><dt>Correlation ID</dt><dd className="font-mono">{item.correlationId}</dd></div></dl>
      {(item.oldValues || item.newValues) && <details className="mt-2"><summary>View change details</summary>{item.oldValues && <pre>Before: {item.oldValues}</pre>}{item.newValues && <pre>After: {item.newValues}</pre>}</details>}
    </article>)}</div>}
    {!loading && !data && !!error && <div className="panel"><EmptyState title="Audit unavailable" detail="Check the connection and use Refresh audit to try again." icon="alert" /></div>}
    <Pagination page={page} total={data?.total ?? 0} noun="entries" disabled={loading} onPage={setPage} />
  </>
}
