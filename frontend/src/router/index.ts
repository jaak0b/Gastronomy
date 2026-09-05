import { ref, type Ref } from 'vue'
import { assertNever } from '../core/assertNever'

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
  | { name: 'openItems' }
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
  if (first === 'open-items') {
    return { name: 'openItems' }
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

function isAServerScreen(route: AppRoute): boolean {
  switch (route.name) {
    case 'home':
    case 'review':
    case 'openItems':
      return true
    case 'enrolQr':
    case 'stations':
    case 'admin':
      return false
    default:
      return assertNever(route)
  }
}

function isTheFirstServerScreen(route: AppRoute): boolean {
  switch (route.name) {
    case 'home':
      return true
    case 'review':
    case 'openItems':
    case 'enrolQr':
    case 'stations':
    case 'admin':
      return false
    default:
      return assertNever(route)
  }
}

function repeatCurrentHistoryEntry(): void {
  const address = window.location.pathname + window.location.search + window.location.hash
  window.history.pushState({}, '', address)
}

function keepAWayBackInsideTheApp(route: AppRoute): void {
  if (isAServerScreen(route)) {
    repeatCurrentHistoryEntry()
  }
}

export function navigate(path: string): void {
  window.history.pushState({}, '', path)
  currentRoute.value = resolveRoute(path)
}

export function replace(path: string): void {
  window.history.replaceState({}, '', path)
  currentRoute.value = resolveRoute(path)
  keepAWayBackAsSoonAsTheScreenIsTouched()
}

function keepAWayBackAsSoonAsTheScreenIsTouched(): void {
  function armOnFirstTouch(): void {
    window.removeEventListener('pointerdown', armOnFirstTouch)
    window.removeEventListener('keydown', armOnFirstTouch)
    keepAWayBackInsideTheApp(currentRoute.value)
  }

  window.addEventListener('pointerdown', armOnFirstTouch)
  window.addEventListener('keydown', armOnFirstTouch)
}

export function startRouter(): void {
  keepAWayBackAsSoonAsTheScreenIsTouched()
  window.addEventListener('popstate', () => {
    currentRoute.value = resolveRoute(window.location.pathname)
    if (isTheFirstServerScreen(currentRoute.value)) {
      repeatCurrentHistoryEntry()
    }
  })
}
