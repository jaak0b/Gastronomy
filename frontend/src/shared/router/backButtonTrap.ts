import { assertNever } from '../core/assertNever'
import type { AppRoute } from './route'

export const BACK_BUTTON_TRAP_RETURN_PATH_KEY = 'theDoorTarget'
export const BACK_BUTTON_TRAP_FRESH_START_KEY = 'arrivedThroughTheDoor'
export const BACK_BUTTON_TRAP_ANCHOR_KEY = 'theDoorAnchor'

let trapIsActive = false

let thisDocumentIsTheAnchor = false

function currentAddress(): string {
  return window.location.pathname + window.location.search + window.location.hash
}

function trapChainUrl(): string {
  return '/door.html?n=10&v=' + encodeURIComponent(document.lastModified || '')
}

function returnPathFor(route: AppRoute): string {
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

function storeReturnPathIfTrapActive(route: AppRoute): void {
  if (!trapIsActive) {
    return
  }
  sessionStorage.setItem(BACK_BUTTON_TRAP_RETURN_PATH_KEY, returnPathFor(route))
}

function rebuildBackButtonTrapOnRestore(event: PageTransitionEvent): void {
  if (backButtonTrapNeedsRebuildOnRestore(event.persisted)) {
    window.location.assign(trapChainUrl())
  }
}

export function backButtonTrapNeedsBuilding(): boolean {
  return sessionStorage.getItem(BACK_BUTTON_TRAP_ANCHOR_KEY) !== 'yes'
}

export function buildBackButtonTrap(): void {
  sessionStorage.setItem(BACK_BUTTON_TRAP_ANCHOR_KEY, 'yes')
  sessionStorage.setItem(BACK_BUTTON_TRAP_RETURN_PATH_KEY, currentAddress())
  thisDocumentIsTheAnchor = true
  window.location.assign(trapChainUrl())
}

export function backButtonTrapNeedsRebuildOnRestore(persisted: boolean): boolean {
  return persisted && thisDocumentIsTheAnchor
}

export function backButtonTrapIsActive(): boolean {
  return trapIsActive
}

export function markBackButtonTrapActive(route: AppRoute): void {
  if (trapIsActive) {
    return
  }
  trapIsActive = true
  storeReturnPathIfTrapActive(route)
}

export function noteRouteChangeForBackButtonTrap(route: AppRoute): void {
  storeReturnPathIfTrapActive(route)
}

export function prepareBackButtonTrapForFreshStart(path: string): void {
  sessionStorage.setItem(BACK_BUTTON_TRAP_ANCHOR_KEY, 'yes')
  sessionStorage.setItem(BACK_BUTTON_TRAP_RETURN_PATH_KEY, path)
  sessionStorage.setItem(BACK_BUTTON_TRAP_FRESH_START_KEY, 'yes')
}

export function startBackButtonTrap(): void {
  window.addEventListener('pageshow', rebuildBackButtonTrapOnRestore)
}
