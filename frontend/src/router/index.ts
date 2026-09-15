import { ref, type Ref } from 'vue'
import { assertNever } from '../core/assertNever'
import { resolveRoute, type AppRoute } from '../core/route'
import { plantTheBackGuardPad } from '../theBackGuardPad'

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
  if (!theDeviceIsKeptInsideTheApp) {
    currentRoute.value = resolveRoute(window.location.pathname)
    theAddressOfTheScreen = currentAddress()
    return
  }
  if (closeTheOpenStep !== null) {
    closeTheStepInsideTheScreen()
  } else {
    goOneScreenBack()
  }
  window.history.pushState(null, '', theAddressOfTheScreen)
}

function goOneScreenBack(): void {
  switch (currentRoute.value.name) {
    case 'review':
    case 'openItems':
      navigate('/')
      return
    case 'home':
    case 'stations':
    case 'enrolQr':
    case 'admin':
      return
    default:
      return assertNever(currentRoute.value)
  }
}

function keepTheGuardAfterTheBrowserRestoredThePage(event: PageTransitionEvent): void {
  if (event.persisted && theDeviceIsKeptInsideTheApp) {
    window.history.pushState(null, '', theAddressOfTheScreen)
  }
}

export function keepTheDeviceInsideTheApp(): void {
  startRouter()
  if (theDeviceIsKeptInsideTheApp) {
    return
  }
  theDeviceIsKeptInsideTheApp = true
  theAddressOfTheScreen = currentAddress()
  plantTheBackGuardPad()
}

export function navigate(path: string): void {
  closeTheStepInsideTheScreen()
  if (theDeviceIsKeptInsideTheApp) {
    window.history.replaceState(null, '', path)
  } else {
    window.history.pushState(null, '', path)
  }
  currentRoute.value = resolveRoute(path)
  theAddressOfTheScreen = currentAddress()
}

export function startOverAt(path: string): void {
  window.location.replace(path)
}
