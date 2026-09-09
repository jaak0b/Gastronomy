import type { DeviceKind } from './apiTypes'
import type { AppRoute } from './route'
import { assertNever } from './assertNever'

export type ScreenName =
  | 'enrolQr'
  | 'welcome'
  | 'startingUp'
  | 'catalog'
  | 'review'
  | 'openItems'
  | 'station'
  | 'admin'

export type DeviceSession =
  | { state: 'notSetUp' }
  | { state: 'startingUp' }
  | { state: 'setUp'; deviceKind: DeviceKind }

type EnrolledScreenRoute = 'home' | 'review' | 'openItems' | 'stations'

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

function screenForTheKindOfDevice(
  deviceKind: DeviceKind,
  route: EnrolledScreenRoute,
): ScreenName {
  switch (deviceKind) {
    case 'station':
      return 'station'
    case 'staffMember':
      return screenForAWaiterPhone(route)
    default:
      return assertNever(deviceKind)
  }
}

function screenForTheDeviceSession(
  session: DeviceSession,
  route: EnrolledScreenRoute,
): ScreenName {
  switch (session.state) {
    case 'notSetUp':
      return 'welcome'
    case 'startingUp':
      return 'startingUp'
    case 'setUp':
      return screenForTheKindOfDevice(session.deviceKind, route)
    default:
      return assertNever(session)
  }
}

export function screenFor(session: DeviceSession, route: AppRoute): ScreenName {
  switch (route.name) {
    case 'enrolQr':
      return 'enrolQr'
    case 'admin':
      return 'admin'
    case 'home':
    case 'review':
    case 'openItems':
    case 'stations':
      return screenForTheDeviceSession(session, route.name)
    default:
      return assertNever(route)
  }
}
