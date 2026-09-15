import { resolveRoute } from './core/route'

export const THE_BACK_GUARD_PAD_SIZE = 20

type ThePageThatPlantedThePad = Window & { theBackGuardPadIsPlanted?: boolean }

let thePadIsPlanted = false

export function plantTheBackGuardPad(): void {
  const thePage = window as ThePageThatPlantedThePad
  if (thePadIsPlanted || thePage.theBackGuardPadIsPlanted === true) {
    return
  }
  if (resolveRoute(window.location.pathname).name === 'admin') {
    return
  }
  thePadIsPlanted = true
  thePage.theBackGuardPadIsPlanted = true
  const address = window.location.pathname + window.location.search + window.location.hash
  for (let copy = 0; copy < THE_BACK_GUARD_PAD_SIZE; copy += 1) {
    window.history.pushState(null, '', address)
  }
}
