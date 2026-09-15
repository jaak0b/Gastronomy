import { ref, type Ref } from 'vue'
import { assertNever } from '../core/assertNever'
import { resolveRoute, type AppRoute } from '../core/route'

export { ADMIN_SECTIONS, resolveRoute } from '../core/route'
export type { AdminSection, AppRoute } from '../core/route'

export const THE_DOOR_TARGET_KEY = 'theDoorTarget'
export const THE_DOOR_ARRIVAL_KEY = 'arrivedThroughTheDoor'
export const THE_DOOR_ANCHOR_KEY = 'theDoorAnchor'

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

let theDeviceIsBehindADoor = false

let thisDocumentIsTheAnchor = false

function currentAddress(): string {
  return window.location.pathname + window.location.search + window.location.hash
}

function theDoorChainAddress(): string {
  return '/door.html?n=10&v=' + encodeURIComponent(document.lastModified || '')
}

export function needsTheDoorOpened(): boolean {
  if (resolveRoute(window.location.pathname).name === 'admin') {
    return false
  }
  return sessionStorage.getItem(THE_DOOR_ANCHOR_KEY) !== 'yes'
}

export function openTheDoor(): void {
  sessionStorage.setItem(THE_DOOR_ANCHOR_KEY, 'yes')
  sessionStorage.setItem(THE_DOOR_TARGET_KEY, currentAddress())
  thisDocumentIsTheAnchor = true
  window.location.assign(theDoorChainAddress())
}

function theDoorTargetFor(route: AppRoute): string {
  switch (route.name) {
    case 'home':
    case 'review':
    case 'openItems':
      return '/'
    case 'stations':
      return '/stations'
    case 'enrolQr':
    case 'admin':
      return currentAddress()
    default:
      return assertNever(route)
  }
}

function rememberTheDoorTarget(route: AppRoute): void {
  if (!theDeviceIsBehindADoor) {
    return
  }
  sessionStorage.setItem(THE_DOOR_TARGET_KEY, theDoorTargetFor(route))
}

export function startRouter(): void {
  window.addEventListener('popstate', followTheBrowser)
  window.addEventListener('pageshow', rebuildTheDoorWhenTheAnchorComesBack)
}

function followTheBrowser(): void {
  currentRoute.value = resolveRoute(window.location.pathname)
  rememberTheDoorTarget(currentRoute.value)
  closeTheStepInsideTheScreen()
}

function rebuildTheDoorWhenTheAnchorComesBack(event: PageTransitionEvent): void {
  if (event.persisted && thisDocumentIsTheAnchor) {
    window.location.assign(theDoorChainAddress())
  }
}

export function keepTheDeviceBehindTheDoor(): void {
  if (theDeviceIsBehindADoor) {
    return
  }
  theDeviceIsBehindADoor = true
  rememberTheDoorTarget(currentRoute.value)
}

export function navigate(path: string): void {
  closeTheStepInsideTheScreen()
  if (theDeviceIsBehindADoor) {
    window.history.replaceState(null, '', path)
  } else {
    window.history.pushState(null, '', path)
  }
  currentRoute.value = resolveRoute(path)
  rememberTheDoorTarget(currentRoute.value)
}

export function startOverAt(path: string): void {
  sessionStorage.setItem(THE_DOOR_TARGET_KEY, path)
  sessionStorage.setItem(THE_DOOR_ARRIVAL_KEY, 'yes')
  window.location.replace(path)
}
