import { ref, type Ref } from 'vue'

export type AdminSection =
  | 'overview'
  | 'locations'
  | 'items'
  | 'assignment'
  | 'printers'
  | 'people'
  | 'event'

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
  'assignment',
  'printers',
  'people',
  'event',
]

function adminSectionFrom(segment: string | undefined): AdminSection {
  const match = ADMIN_SECTIONS.find((section) => section === segment)
  return match ?? 'overview'
}

export function resolveRoute(path: string): AppRoute {
  const segments = path.split('?')[0].split('/').filter((segment) => segment.length > 0)
  if (segments.length === 0) {
    return { name: 'home' }
  }
  if (segments[0] === 'j' && segments.length >= 2) {
    return { name: 'enrolQr', code: segments[1] }
  }
  if (segments[0] === 'review') {
    return { name: 'review' }
  }
  if (segments[0] === 'orders') {
    return segments.length >= 2
      ? { name: 'orderDetail', orderId: segments[1] }
      : { name: 'orders' }
  }
  if (segments[0] === 'station' && segments.length >= 2) {
    return { name: 'station', accessKey: segments[1] }
  }
  if (segments[0] === 'admin') {
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
