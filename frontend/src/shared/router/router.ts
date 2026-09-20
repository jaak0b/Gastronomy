import { ref, type Ref } from 'vue'
import { resolveRoute, type AppRoute } from './route'
import {
  backButtonTrapIsActive,
  noteRouteChangeForBackButtonTrap,
  prepareBackButtonTrapForFreshStart,
  startBackButtonTrap,
} from './backButtonTrap'

export { ADMIN_SECTIONS, resolveRoute } from './route'
export type { AdminSection, AppRoute } from './route'

export const currentRoute: Ref<AppRoute> = ref(resolveRoute(window.location.pathname))

let closeTheOpenStep: (() => void) | null = null

export function registerOpenStepCloser(close: () => void): void {
  closeTheOpenStep = close
}

export function closeOpenStep(): void {
  const close = closeTheOpenStep
  closeTheOpenStep = null
  close?.()
}

function handlePopState(): void {
  currentRoute.value = resolveRoute(window.location.pathname)
  noteRouteChangeForBackButtonTrap(currentRoute.value)
  closeOpenStep()
}

export function startRouter(): void {
  window.addEventListener('popstate', handlePopState)
  startBackButtonTrap()
}

export function navigate(path: string): void {
  closeOpenStep()
  if (backButtonTrapIsActive()) {
    window.history.replaceState(null, '', path)
  } else {
    window.history.pushState(null, '', path)
  }
  currentRoute.value = resolveRoute(path)
  noteRouteChangeForBackButtonTrap(currentRoute.value)
}

export function startOverAt(path: string): void {
  prepareBackButtonTrapForFreshStart(path)
  window.location.replace(path)
}
