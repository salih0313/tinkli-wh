import { useEffect, useRef, useState } from 'react'
import { Dialog } from '../components/ui/Dialog'
import { Button } from '../components/ui/Controls'

export function useConfirm() {
  const [message, setMessage] = useState('')
  const [title,setTitle]=useState('Confirm change')
  const [label,setLabel]=useState('Confirm change')
  const resolve = useRef<((answer: boolean) => void) | null>(null)
  useEffect(() => () => resolve.current?.(false), [])
  function finish(answer: boolean) { resolve.current?.(answer); resolve.current = null; setMessage('') }
  function confirm(detail: string | {title:string;message:string;confirmLabel?:string;danger?:boolean}) {
    setTitle(typeof detail==='string'?'Confirm change':detail.title);setLabel(typeof detail==='string'?'Confirm change':detail.confirmLabel??'Confirm change')
    return new Promise<boolean>(done => { resolve.current?.(false); resolve.current = done; setMessage(typeof detail==='string'?detail:detail.message) })
  }
  return { confirm, confirmation: <Dialog open={!!message} title={title} description={message} onClose={() => finish(false)} className="confirm-dialog">
    <div className="dialog-actions"><Button variant="secondary" autoFocus onClick={() => finish(false)}>Cancel</Button><Button variant="danger" onClick={() => finish(true)}>{label}</Button></div>
  </Dialog> }
}
