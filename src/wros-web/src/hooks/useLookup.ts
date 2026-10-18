import { useEffect, useState } from 'react'
import { get } from '../api/client'
import type { Page } from '../api/types'
// Fetch every lookup page; never silently truncate a supplier/product/location selector at 100 rows.
export async function allPages<T>(path: string, signal?: AbortSignal): Promise<T[]> {
  const result: T[] = []; let page = 1
  while (true) {
    const data = await get<Page<T>>(`${path}${path.includes('?') ? '&' : '?'}page=${page}&pageSize=100`, signal)
    result.push(...data.items)
    if (page * data.pageSize >= data.total) return result
    page += 1
  }
}
export function useLookup<T>(path: string | null) {
  const [revision, setRevision] = useState(0)
  const [items, setItems] = useState<T[]>([])
  const [error, setError] = useState<unknown>(null)
  const [loading, setLoading] = useState(true)
  useEffect(() => {
    if (!path) { setItems([]); setLoading(false); return }
    const controller = new AbortController(); setLoading(true); setError(null)
    allPages<T>(path, controller.signal).then(setItems).catch(failure => { if (!controller.signal.aborted) setError(failure) })
      .finally(() => { if (!controller.signal.aborted) setLoading(false) })
    return () => controller.abort()
  }, [path, revision])
  return { items, error, loading, refresh: () => setRevision(value => value + 1) }
}
