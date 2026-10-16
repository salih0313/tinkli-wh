import { useEffect, useState } from 'react'
import { get } from '../api/client'
import type { AuditEntry, Page } from '../api/types'
import { ErrorMessage, Heading } from '../components/Form'
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
  return <><Heading title="Security audit" detail="Account, authentication and permission events. Timestamps are shown in UTC." />
    <ErrorMessage error={error} /><button className="secondary mb-4" disabled={loading} onClick={() => setRevision(value => value + 1)}>Refresh audit</button>
    {loading ? <p role="status">Loading audit…</p> : data?.items.length === 0 ? <p className="panel p-6">No audit entries yet.</p> : <div className="space-y-3">{data?.items.map(item => <article className="panel break-words p-5" key={item.id}>
      <div className="flex flex-wrap justify-between gap-2"><h2 className="font-semibold">{item.action}</h2><time className="text-sm text-slate-500" dateTime={item.timestamp}>{new Date(item.timestamp).toISOString()}</time></div>
      <dl className="mt-3 grid gap-2 text-sm sm:grid-cols-2"><div><dt className="text-slate-500">Actor</dt><dd className="break-all font-mono text-xs">{item.actorId ?? 'Anonymous / system'}</dd></div><div><dt className="text-slate-500">Entity</dt><dd className="break-all">{item.entity} {item.entityId}</dd></div><div className="sm:col-span-2"><dt className="text-slate-500">Correlation ID</dt><dd className="break-all font-mono text-xs">{item.correlationId}</dd></div></dl>
      {(item.oldValues || item.newValues) && <details className="mt-3 text-sm"><summary className="cursor-pointer text-teal-700">Safe change details</summary>{item.oldValues && <pre className="mt-2 whitespace-pre-wrap break-all text-xs">Before: {item.oldValues}</pre>}{item.newValues && <pre className="mt-2 whitespace-pre-wrap break-all text-xs">After: {item.newValues}</pre>}</details>}
    </article>)}</div>}
    <div className="mt-5 flex items-center justify-between text-sm"><span>{data?.total ?? 0} entries · Page {page}</span><div className="flex gap-2"><button className="secondary" disabled={loading || page <= 1} onClick={() => setPage(value => value - 1)}>Previous</button><button className="secondary" disabled={loading || !data || page * 20 >= data.total} onClick={() => setPage(value => value + 1)}>Next</button></div></div>
  </>
}
