import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App'
import { NotificationViewport } from './components/feedback/NotificationViewport'
import './styles.css'

createRoot(document.getElementById('root')!).render(<StrictMode><App /><NotificationViewport /></StrictMode>)
