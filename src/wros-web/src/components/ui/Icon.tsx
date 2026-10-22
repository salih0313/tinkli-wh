import type { SVGProps } from 'react'

const paths = {
  warehouse: 'M3 21V8l9-5 9 5v13M3 10h18M7 21v-7h10v7M10 14v7M3 21h18',
  overview: 'M3 3h7v7H3zM14 3h7v7h-7zM3 14h7v7H3zM14 14h7v7h-7z',
  users: 'M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8M22 21v-2a4 4 0 0 0-3-3.87M16 3.13a4 4 0 0 1 0 7.75',
  shield: 'M12 22s9-4 9-11V5l-9-3-9 3v6c0 7 9 11 9 11ZM8 12l3 3 5-6',
  audit: 'M9 3H5v18h14V3h-4M9 2h6v4H9zM8 11h8M8 15h8M8 18h4',
  key: 'M15 3a6 6 0 0 0-5.65 8L2 18v4h4v-3h3v-3l2.9-2.9A6 6 0 1 0 15 3ZM17 7h.01',
  activity: 'M2 12h5l3-9 4 18 3-9h5',
  menu: 'M4 6h16M4 12h16M4 18h16',
  close: 'm6 6 12 12M6 18 18 6',
  chevron: 'm9 5 7 7-7 7',
  down: 'm6 9 6 6 6-6',
  plus: 'M12 5v14M5 12h14',
  search: 'M21 21l-5-5M10.5 18a7.5 7.5 0 1 0 0-15 7.5 7.5 0 0 0 0 15',
  refresh: 'M20 7v5h-5M4 17v-5h5M6 6a8 8 0 0 1 13 2l1 4M4 12l1 4a8 8 0 0 0 13 2',
  logout: 'M9 4H4v16h5M14 8l4 4-4 4M8 12h13',
  arrow: 'M5 12h14m-6-6 6 6-6 6',
  check: 'm5 12 4 4L19 6',
  alert: 'm12 3 10 18H2L12 3ZM12 9v4M12 17h.01',
  info: 'M12 8h.01M11 12h1v5M22 12a10 10 0 1 0-20 0 10 10 0 0 0 20 0',
  lock: 'M5 11h14v10H5zM8 11V7a4 4 0 0 1 8 0v4',
  eye: 'M2 12s4-7 10-7 10 7 10 7-4 7-10 7S2 12 2 12ZM15 12a3 3 0 1 0-6 0 3 3 0 0 0 6 0',
  eyeOff: 'm3 3 18 18M10.5 5.1 12 5c6 0 10 7 10 7a20 20 0 0 1-3 3.8M6 6.5A24 24 0 0 0 2 12s4 7 10 7a12 12 0 0 0 5-1.2M10 10a3 3 0 0 0 4 4',
  collapse: 'M5 4h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2ZM9 4v16m7-11-3 3 3 3',
  expand: 'M5 4h14a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2ZM9 4v16m4-11 3 3-3 3',
  external: 'M14 3h7v7M21 3 10 14M10 3H3v18h18v-7',
} as const
export type IconName = keyof typeof paths
export function Icon({ name, ...props }: SVGProps<SVGSVGElement> & { name: IconName }) {
  return <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true" {...props}><path d={paths[name]} /></svg>
}
