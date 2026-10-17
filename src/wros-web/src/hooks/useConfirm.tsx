import { useEffect, useRef, useState } from 'react'
import { Dialog } from '../components/ui/Dialog'
import { Button } from '../components/ui/Controls'

export function useConfirm() {
  const [message, setMessage] = useState('')
  const resolve = useRef<((answer: boolean) => void) | null>(null)
  useEffect(() => () => resolve.current?.(false), [])
  function finish(answer: boolean) { resolve.current?.(answer); resolve.current = null; setMessage('') }
  function confirm(detail: string) {
    return new Promise<boolean>(done => { resolve.current?.(false); resolve.current = done; setMessage(detail) })
  }
  return { confirm, confirmation: <Dialog open={!!message} title="Confirm account change" description={message} onClose={() => finish(false)} className="confirm-dialog">
    <div className="dialog-actions"><Button variant="secondary" autoFocus onClick={() => finish(false)}>Cancel</Button><Button variant="danger" onClick={() => finish(true)}>Confirm change</Button></div>
  </Dialog> }
}
