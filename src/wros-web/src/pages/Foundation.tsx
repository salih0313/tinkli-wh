import { useEffect, useState } from 'react'
import { getHealth } from '../api/client'
import type { HealthReport } from '../api/client'
import { Heading } from '../components/Form'

type Result = { report: HealthReport; error?: never } | { error: string; report?: never }
export default function Foundation() {
  const [attempt, setAttempt] = useState(0)
  const [results, setResults] = useState<Result[]>([])
  const [loading, setLoading] = useState(true)
  useEffect(() => {
    const controller = new AbortController(); setLoading(true)
    Promise.all((['live', 'ready'] as const).map(async (kind): Promise<Result> => {
      try { return { report: await getHealth(kind, controller.signal) } }
      catch (error) { return { error: error instanceof Error ? error.message : 'Unable to reach API.' } }
    })).then(value => { if (!controller.signal.aborted) { setResults(value); setLoading(false) } })
    return () => controller.abort()
  }, [attempt])
  return <><Heading title="System status" detail="API liveness and SQL Server connectivity with migration readiness." />
    <div className="grid gap-5 sm:grid-cols-2" aria-live="polite" aria-busy={loading}>{['API process', 'Database readiness'].map((name, index) => {
      const result = results[index]
      return <section key={name} className="panel p-6"><h2 className="font-semibold">{name}</h2>
        <p className={`mt-4 text-lg font-medium ${result?.report?.status === 'Healthy' ? 'text-teal-700' : 'text-amber-700'}`}>{loading ? 'Checking…' : result?.report?.status ?? 'Connection failed'}</p>
        {result?.error && !loading && <p className="mt-2 text-sm text-slate-600">{result.error}</p>}
        {result?.report && !loading && <p className="mt-3 break-all font-mono text-xs text-slate-500">Reference: {result.report.correlationId}</p>}
      </section>
    })}</div>
    <button className="primary mt-6" disabled={loading} onClick={() => setAttempt(value => value + 1)}>Refresh status</button>
    <p className="mt-6 text-sm"><a className="text-button" href="/openapi/v1.json" target="_blank" rel="noreferrer">Open development API specification</a></p>
  </>
}
