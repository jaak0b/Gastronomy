export const MILLISECONDS_A_DOCKED_STRIP_IGNORES_AFTER_A_SCROLL = 400

export const PIXELS_A_FINGER_MAY_TRAVEL_AND_STILL_BE_A_TAP = 10

export interface FingerTravel {
  fromX: number
  fromY: number
  toX: number
  toY: number
}

export interface TapOnADockedStrip {
  atMillisecond: number
  lastScrollAtMillisecond: number | null
  theFingerLeftTheSpotItPressed: boolean
}

export function theFingerLeftTheSpotItPressed(travel: FingerTravel): boolean {
  const sideways = travel.toX - travel.fromX
  const along = travel.toY - travel.fromY
  return Math.hypot(sideways, along) > PIXELS_A_FINGER_MAY_TRAVEL_AND_STILL_BE_A_TAP
}

export function theTapReachesTheButton(tap: TapOnADockedStrip): boolean {
  if (tap.theFingerLeftTheSpotItPressed) {
    return false
  }
  if (tap.lastScrollAtMillisecond === null) {
    return true
  }
  const sinceTheListStopped = tap.atMillisecond - tap.lastScrollAtMillisecond
  return sinceTheListStopped >= MILLISECONDS_A_DOCKED_STRIP_IGNORES_AFTER_A_SCROLL
}
