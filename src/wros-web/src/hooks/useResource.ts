import { useEffect, useState } from 'react'
import { get } from '../api/client'
export function useResource<T>(url: string | null) {
  const [data, setData] = useState<T | null>(null),
    [loading, setLoading] = useState(true),
    [error, setError] = useState<unknown>(null),
    [revision, setRevision] = useState(0)
  useEffect(() => {
    if (!url) {
      setLoading(false)
      setError(null)
      return
    }
    const controller = new AbortController()
    setLoading(true)
    setData(null)
    setError(null)
    get<T>(url, controller.signal)
      .then(setData)
      .catch((e) => {
        if (!controller.signal.aborted) setError(e)
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [url, revision])
  return { data, loading, error, refresh: () => setRevision((v) => v + 1) }
}
