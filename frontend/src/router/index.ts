import { ref, type Ref } from 'vue'

export type AdminSection =
  | 'overview'
  | 'stations'
  | 'items'
  | 'printers'
  | 'staff'

export type AppRoute =
  | { name: 'enrolQr'; code: string }
  | { name: 'home' }
  | { name: 'review' }
  | { name: 'stations' }
  | { name: 'admin'; section: AdminSection }

const ADMIN_SECTIONS: AdminSection[] = [
  'overview',
  'stations',
  'items',
  'printers',
  'staff',
]

function adminSectionFrom(segment: string | undefined): AdminSection {
  const wanted = (segment ?? '').toLowerCase()
  const match = ADMIN_SECTIONS.find((section) => section === wanted)
  return match ?? 'overview'
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
  if (first === 'stations') {
    return { name: 'stations' }
  }
  if (first === 'admin') {
    return { name: 'admin', section: adminSectionFrom(segments[1]) }
  }
  return { name: 'home' }
}

export const currentRoute: Ref<AppRoute> = ref(resolveRoute(window.location.pathname))

export function navigate(path: string): void {
  window.history.pushState({}, '', path)
  currentRoute.value = resolveRoute(path)
}

export function startRouter(): void {
  window.addEventListener('popstate', () => {
    currentRoute.value = resolveRoute(window.location.pathname)
  })
}
