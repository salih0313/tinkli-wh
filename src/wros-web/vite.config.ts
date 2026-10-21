import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

const proxy = { '/api': { target: 'https://localhost:7243', secure: false }, '/openapi': { target: 'https://localhost:7243', secure: false } }

// Development proxy only. Separate production API origin is configured in public/config.js.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: { host: 'localhost', port: 5173, strictPort: true, proxy },
  preview: { host: 'localhost', port: 4173, strictPort: true, proxy },
})
