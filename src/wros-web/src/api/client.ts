export class ApiError extends Error {
  constructor(public status: number, message: string, public correlationId?: string) { super(message) }
}

// Loaded before React from public/config.js; editable on the host without rebuilding.
const configuredUrl = (window as Window & { WROS_CONFIG?: { apiBaseUrl?: string } }).WROS_CONFIG?.apiBaseUrl?.trim() ?? ''
function validateApiBaseUrl(value: string): string {
  if (!value) return ''
  const url = new URL(value)
  if (url.username || url.password || url.search || url.hash || url.pathname !== '/'
    || (url.protocol !== 'https:' && !(url.protocol === 'http:' && ['localhost', '127.0.0.1'].includes(url.hostname)))) {
    throw new Error('API URL must be an HTTPS origin without a path, credentials, query or fragment.')
  }
  return url.origin
}
export const apiBaseUrl = validateApiBaseUrl(configuredUrl)

async function request(path: string, init: RequestInit = {}): Promise<Response> {
  let response: Response
  try {
    response = await fetch(`${apiBaseUrl}${path}`, {
      ...init, credentials: 'include',
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
  return response
}

async function send<T>(path: string, init: RequestInit = {}): Promise<T> {
  const response = await request(path, init)
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export function get<T>(path: string, signal?: AbortSignal): Promise<T> { return send<T>(path, { signal }) }

export async function download(path: string, fileName: string): Promise<void> {
  const response = await request(path, { headers: { Accept: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' } })
  const url = URL.createObjectURL(await response.blob())
  const link = document.createElement('a')
  link.href = url
  link.download = fileName
  document.body.appendChild(link)
  try { link.click() } finally {
    link.remove()
    // Allow the browser to start reading the file before releasing its URL.
    setTimeout(() => URL.revokeObjectURL(url), 60000)
  }
}

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
  const response = await fetch(`${apiBaseUrl}/api/v1/health/${kind}`, { credentials: 'include', signal: AbortSignal.any([signal, AbortSignal.timeout(15000)]) })
  if (!response.ok && response.status !== 503) throw new ApiError(response.status, 'Health check failed.')
  return response.json() as Promise<HealthReport>
}

export async function upload<T>(path: string, file: File): Promise<T> {
  const csrf = await get<{ token: string }>('/api/v1/auth/csrf')
  const body = new FormData(); body.append('file', file)
  return send<T>(path, { method: 'POST', headers: { 'X-CSRF-TOKEN': csrf.token }, body })
}
