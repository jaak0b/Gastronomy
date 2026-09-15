import { ref, type Ref } from 'vue'
import { resolveRoute, type AppRoute } from '../core/route'

export { ADMIN_SECTIONS, resolveRoute } from '../core/route'
export type { AdminSection, AppRoute } from '../core/route'

export const currentRoute: Ref<AppRoute> = ref(resolveRoute(window.location.pathname))

let closeTheOpenStep: (() => void) | null = null

export function openAStepInsideTheScreen(close: () => void): void {
  closeTheOpenStep = close
}

export function closeTheStepInsideTheScreen(): void {
  const close = closeTheOpenStep
  closeTheOpenStep = null
  close?.()
}

function currentAddress(): string {
  return window.location.pathname + window.location.search + window.location.hash
}

let theAddressOfTheScreen = currentAddress()

let theDeviceIsKeptInsideTheApp = false

let theRouterIsListening = false

export function startRouter(): void {
  if (theRouterIsListening) {
    return
  }
  theRouterIsListening = true
  window.addEventListener('popstate', followTheBrowserBackButton)
  window.addEventListener('pageshow', keepTheGuardAfterTheBrowserRestoredThePage)
}

function followTheBrowserBackButton(): void {
  if (theDeviceIsKeptInsideTheApp && closeTheOpenStep !== null) {
    closeTheStepInsideTheScreen()
    window.history.pushState({ theApp: true }, '', theAddressOfTheScreen)
    return
  }
  const landed = window.history.state as { floor?: boolean } | null
  if (theDeviceIsKeptInsideTheApp && (landed === null || landed.floor === true)) {
    window.history.pushState({ theApp: true }, '', currentAddress())
    return
  }
  currentRoute.value = resolveRoute(window.location.pathname)
  theAddressOfTheScreen = currentAddress()
}

function keepTheGuardAfterTheBrowserRestoredThePage(event: PageTransitionEvent): void {
  if (event.persisted && theDeviceIsKeptInsideTheApp) {
    window.history.pushState({ theApp: true }, '', currentAddress())
  }
}

export function keepTheDeviceInsideTheApp(): void {
  startRouter()
  if (theDeviceIsKeptInsideTheApp) {
    return
  }
  theDeviceIsKeptInsideTheApp = true
  theAddressOfTheScreen = currentAddress()
  window.history.replaceState({ theApp: true, floor: true }, '', currentAddress())
  window.history.pushState({ theApp: true }, '', currentAddress())
}

export function navigate(path: string): void {
  closeTheStepInsideTheScreen()
  window.history.pushState({ theApp: true }, '', path)
  currentRoute.value = resolveRoute(path)
  theAddressOfTheScreen = currentAddress()
}

export function replace(path: string): void {
  window.history.replaceState({ theApp: true }, '', path)
  currentRoute.value = resolveRoute(path)
  theAddressOfTheScreen = currentAddress()
}

export function startOverAt(path: string): void {
  window.location.replace(path)
}
