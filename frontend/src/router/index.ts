import { ref, type Ref } from 'vue'
import { assertNever } from '../core/assertNever'
import { resolveRoute, type AppRoute } from '../core/route'

export { ADMIN_SECTIONS, resolveRoute } from '../core/route'
export type { AdminSection, AppRoute } from '../core/route'

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
