export type AdminSection = 'festivals' | 'overview' | 'stations' | 'items' | 'staff'

export type AppRoute =
  | { name: 'enrolQr'; code: string }
  | { name: 'home' }
  | { name: 'review' }
  | { name: 'openItems' }
  | { name: 'stations' }
  | { name: 'admin'; section: AdminSection; festivalId: string | null }

const ADMIN_SECTIONS: AdminSection[] = ['overview', 'festivals', 'stations', 'items', 'staff']

function adminSectionFrom(segment: string | undefined): AdminSection {
  const wanted = (segment ?? '').toLowerCase()
  const match = ADMIN_SECTIONS.find((section) => section === wanted)
  return match ?? 'festivals'
}

function adminRouteFrom(segments: string[]): AppRoute {
  const section = adminSectionFrom(segments[1])
  const festivalId = section === 'festivals' ? (segments[2] ?? null) : null
  return { name: 'admin', section, festivalId }
}

export function resolveRoute(path: string): AppRoute {
  const segments = path
    .split('?')[0]
    .split('#')[0]
    .split('/')
    .filter((segment) => segment.length > 0)
  const first = (segments[0] ?? '').toLowerCase()
  if (segments.length === 0) {
    return { name: 'home' }
  }
  if (first === 'j' && segments.length >= 2) {
    return { name: 'enrolQr', code: segments[1] }
  }
  if (first === 'review') {
    return { name: 'review' }
  }
  if (first === 'open-items') {
    return { name: 'openItems' }
  }
  if (first === 'stations') {
    return { name: 'stations' }
  }
  if (first === 'admin') {
    return adminRouteFrom(segments)
  }
  return { name: 'home' }
}

export { ADMIN_SECTIONS }
