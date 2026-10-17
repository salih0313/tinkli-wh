import { useState } from 'react'
import type { InputHTMLAttributes } from 'react'
import { IconButton } from '../ui/Controls'
export function PasswordInput(props: Omit<InputHTMLAttributes<HTMLInputElement>, 'type'>) {
  const [visible, setVisible] = useState(false)
  return <span className="password-control"><input {...props} type={visible ? 'text' : 'password'} />
    <IconButton icon={visible ? 'eyeOff' : 'eye'} label={`${visible ? 'Hide' : 'Show'} ${props['aria-label'] ?? 'password'}`} aria-pressed={visible} onClick={() => setVisible(value => !value)} />
  </span>
}
