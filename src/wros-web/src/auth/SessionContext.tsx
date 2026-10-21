import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { ApiError, get } from '../api/client'
import type { Session } from '../api/types'

interface SessionState { user: Session | null; loading: boolean; error: string; refresh: () => Promise<Session | null>; clear: () => void }
const Context = createContext<SessionState | null>(null)
export function SessionProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<Session | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState('')
  const clear = useCallback(() => setUser(null), [])
  const refresh = useCallback(async () => {
    try { const session = await get<Session>('/api/v1/auth/me'); setUser(session); setError(''); return session }
    catch (failure) {
      if (failure instanceof ApiError && failure.status === 401) { setUser(null); setError(''); return null }
      else { setError(failure instanceof Error ? failure.message : 'Session check failed.'); throw failure }
    } finally { setLoading(false) }
  }, [])
  useEffect(() => {
    void refresh().catch(() => {})
    window.addEventListener('wros:unauthorized', clear)
    return () => window.removeEventListener('wros:unauthorized', clear)
  }, [refresh, clear])
  const signedIn = user !== null
  useEffect(() => {
    if (!signedIn) return
    const focus = () => { void refresh().catch(() => {}) }
    window.addEventListener('focus', focus)
    return () => window.removeEventListener('focus', focus)
  }, [signedIn, refresh])
  useEffect(() => {
    if (!user?.expiresAt) return
    const remaining = new Date(user.expiresAt).getTime() - Date.now()
    if (remaining <= 0) { clear(); return }
    const timer = window.setTimeout(clear, remaining)
    return () => window.clearTimeout(timer)
  }, [user, clear])
  return <Context.Provider value={{ user, loading, error, refresh, clear }}>{children}</Context.Provider>
}
export function useSession() {
  const value = useContext(Context)
  if (!value) throw new Error('SessionProvider is required.')
  return value
}
