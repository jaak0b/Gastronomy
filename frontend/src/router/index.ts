import { ref, type Ref } from 'vue'

export type AdminSection =
  | 'overview'
  | 'locations'
  | 'items'
  | 'printers'
  | 'people'

export type AppRoute =
  | { name: 'enrolQr'; code: string }
  | { name: 'home' }
  | { name: 'review' }
  | { name: 'orders' }
  | { name: 'orderDetail'; orderId: string }
  | { name: 'station'; accessKey: string }
  | { name: 'admin'; section: AdminSection }

const ADMIN_SECTIONS: AdminSection[] = [
  'overview',
  'locations',
  'items',
  'printers',
  'people',
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
  if (first === 'orders') {
    return segments.length >= 2
      ? { name: 'orderDetail', orderId: segments[1] }
      : { name: 'orders' }
  }
  if (first === 'station' && segments.length >= 2) {
    return { name: 'station', accessKey: segments[1] }
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
