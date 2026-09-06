import type { DeviceKind } from './apiTypes'
import type { AppRoute } from './route'
import { assertNever } from './assertNever'

export type ScreenName =
  | 'enrolQr'
  | 'welcome'
  | 'catalog'
  | 'review'
  | 'openItems'
  | 'station'
  | 'admin'

type EnrolledScreenRoute = 'home' | 'review' | 'openItems' | 'stations'

export function parseDeviceKind(stored: string | null): DeviceKind {
  return stored === 'station' ? 'station' : 'staffMember'
}

function screenForAWaiterPhone(route: EnrolledScreenRoute): ScreenName {
  switch (route) {
    case 'home':
      return 'catalog'
    case 'review':
      return 'review'
    case 'openItems':
      return 'openItems'
    case 'stations':
      return 'catalog'
    default:
      return assertNever(route)
  }
}

function screenForAnEnrolledDevice(
  deviceKind: DeviceKind | null,
  route: EnrolledScreenRoute,
): ScreenName {
  if (deviceKind === null) {
    return 'welcome'
  }
  switch (deviceKind) {
    case 'station':
      return 'station'
    case 'staffMember':
      return screenForAWaiterPhone(route)
    default:
      return assertNever(deviceKind)
  }
}

export function screenFor(deviceKind: DeviceKind | null, route: AppRoute): ScreenName {
  switch (route.name) {
    case 'enrolQr':
      return 'enrolQr'
    case 'admin':
      return 'admin'
    case 'home':
    case 'review':
    case 'openItems':
    case 'stations':
      return screenForAnEnrolledDevice(deviceKind, route.name)
    default:
      return assertNever(route)
  }
}
