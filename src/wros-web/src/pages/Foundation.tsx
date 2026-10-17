import { useEffect, useState } from 'react'
import { getHealth } from '../api/client'
import type { HealthReport } from '../api/client'
import { Heading } from '../components/Form'
import { Card, SectionHeader } from '../components/ui/Card'
import { Badge, Button } from '../components/ui/Controls'
import { Icon } from '../components/ui/Icon'
import { LoadingSkeleton } from '../components/feedback/States'

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
  return <><Heading eyebrow="Workspace settings" title="System status" detail="Connection health for the services that power your workspace." action={<Button variant="secondary" icon="refresh" loading={loading} onClick={() => setAttempt(value => value + 1)}>Refresh status</Button>} />
    <div className="grid gap-5 md:grid-cols-2" aria-live="polite" aria-busy={loading}>{['API process', 'Database readiness'].map((name, index) => {
      const result = results[index]
      const healthy = result?.report?.status === 'Healthy'
      return <section key={name} className="panel health-card"><div className="health-title"><div><p className="eyebrow">{index === 0 ? 'Application service' : 'Persistence service'}</p><h2>{name}</h2></div><span className="stat-icon"><Icon name={index === 0 ? 'activity' : 'warehouse'} /></span></div>
        {loading ? <LoadingSkeleton label={`Checking ${name}…`} rows={1} /> : <><Badge tone={healthy ? 'success' : 'danger'}>{result?.report?.status ?? 'Connection failed'}</Badge><p className="health-description">{result?.error ?? (index === 0 ? 'API liveness check. Confirms the application is responding.' : 'SQL Server connectivity and database migration readiness.')}</p>
        {result?.report && <p className="health-reference">Reference: {result.report.correlationId}</p>}</>}
      </section>
    })}</div>
    <Card className="mt-6"><SectionHeader title="Developer resources" detail="Explore the API specification available in the Development environment." action={<a className="text-button" href="/openapi/v1.json" target="_blank" rel="noreferrer">API specification<Icon name="external" width="15" height="15" /><span className="sr-only"> (opens in a new tab)</span></a>} /></Card>
  </>
}
