import { useEffect } from 'react'

// A confirmation can sit above an editor. Restore scrolling only after the last overlay closes.
let locks = 0
let previousOverflow = ''
export function useBodyScrollLock(active: boolean) {
  useEffect(() => {
    if (!active) return
    if (locks === 0) previousOverflow = document.body.style.overflow
    locks += 1
    document.body.style.overflow = 'hidden'
    return () => {
      locks -= 1
      if (locks === 0) document.body.style.overflow = previousOverflow
    }
  }, [active])
}
