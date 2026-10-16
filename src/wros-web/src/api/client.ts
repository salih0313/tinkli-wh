export class ApiError extends Error {
  constructor(public status: number, message: string, public correlationId?: string) { super(message) }
}

// Same-origin requests keep HttpOnly cookies out of browser JavaScript.
// Vite proxies /api and /openapi to the local HTTPS API in Development.
export const apiBaseUrl = ''

async function send<T>(path: string, init: RequestInit = {}): Promise<T> {
  let response: Response
  try {
    response = await fetch(path, {
      ...init, credentials: 'same-origin',
      signal: init.signal ? AbortSignal.any([init.signal, AbortSignal.timeout(15000)]) : AbortSignal.timeout(15000),
      headers: { Accept: 'application/json', 'X-Correlation-ID': crypto.randomUUID(), ...init.headers },
    })
  } catch (error) {
    if (init.signal?.aborted) throw error
    throw new ApiError(0, 'Cannot reach the API. Check the connection and retry.')
  }
  if (!response.ok) {
    const problem = await response.json().catch(() => ({})) as { title?: string; errors?: Record<string, string[]>; correlationId?: string }
    if (response.status === 401) window.dispatchEvent(new Event('wros:unauthorized'))
    const message = response.status === 429 ? 'Too many attempts. Wait a minute and retry.'
      : problem.errors ? Object.values(problem.errors).flat().join(' ') : problem.title
    throw new ApiError(response.status, message ?? (response.status === 429 ? 'Too many attempts. Wait a minute and retry.' : `Request failed (${response.status}).`),
      problem.correlationId ?? response.headers.get('X-Correlation-ID') ?? undefined)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export function get<T>(path: string, signal?: AbortSignal): Promise<T> { return send<T>(path, { signal }) }

export async function mutate<T = void>(method: 'POST' | 'PUT', path: string, body?: unknown): Promise<T> {
  // Fetch a token bound to the current identity before every mutation; never persist it.
  const csrf = await get<{ token: string }>('/api/v1/auth/csrf')
  return send<T>(path, { method, headers: { 'Content-Type': 'application/json', 'X-CSRF-TOKEN': csrf.token },
    body: body === undefined ? undefined : JSON.stringify(body) })
}

export interface HealthReport {
  status: 'Healthy' | 'Degraded' | 'Unhealthy'
  correlationId: string
  checks: { name: string; status: string }[]
}
export async function getHealth(kind: 'live' | 'ready', signal: AbortSignal): Promise<HealthReport> {
  const response = await fetch(`/api/v1/health/${kind}`, { signal: AbortSignal.any([signal, AbortSignal.timeout(15000)]) })
  if (!response.ok && response.status !== 503) throw new ApiError(response.status, 'Health check failed.')
  return response.json() as Promise<HealthReport>
}
