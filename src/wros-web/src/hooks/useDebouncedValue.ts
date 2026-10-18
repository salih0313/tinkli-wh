import { useEffect, useState } from 'react'
export function useDebouncedValue<T>(value: T, milliseconds = 250) {
  const [settled, setSettled] = useState(value)
  useEffect(() => {
    const timer = setTimeout(() => setSettled(value), milliseconds)
    return () => clearTimeout(timer)
  }, [value, milliseconds])
  return settled
}
