import type { AppRoute } from '../router/route'
import { assertNever } from './assertNever'

export type PhoneScreenName = 'tablePlan' | 'catalog' | 'review' | 'openItems' | 'orderOverview'

export type ScreenName =
  | PhoneScreenName
  | 'doorGate'
  | 'enrolQr'
  | 'welcome'
  | 'startingUp'
  | 'station'
  | 'admin'

export function isPhoneScreen(screen: ScreenName): screen is PhoneScreenName {
  switch (screen) {
    case 'tablePlan':
    case 'catalog':
    case 'review':
    case 'openItems':
    case 'orderOverview':
      return true
    case 'doorGate':
    case 'enrolQr':
    case 'welcome':
    case 'startingUp':
    case 'station':
    case 'admin':
      return false
    default:
      return assertNever(screen)
  }
}

export function isDrawnAtPhoneSizes(screen: ScreenName): boolean {
  switch (screen) {
    case 'tablePlan':
    case 'catalog':
    case 'review':
    case 'openItems':
    case 'orderOverview':
    case 'doorGate':
    case 'enrolQr':
    case 'welcome':
    case 'startingUp':
      return true
    case 'station':
    case 'admin':
      return false
    default:
      return assertNever(screen)
  }
}

export type DeviceSession =
  | { state: 'notSetUp' }
  | { state: 'startingUp' }
  | { state: 'waiterPhone' }
  | { state: 'stationTablet' }

type EnrolledScreenRoute = 'home' | 'catalog' | 'review' | 'openItems' | 'orderOverview' | 'stations'

function screenForAWaiterPhone(route: EnrolledScreenRoute): ScreenName {
  switch (route) {
    case 'home':
      return 'tablePlan'
    case 'catalog':
      return 'catalog'
    case 'review':
      return 'review'
    case 'openItems':
      return 'openItems'
    case 'orderOverview':
      return 'orderOverview'
    case 'stations':
      return 'tablePlan'
    default:
      return assertNever(route)
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
    case 'stationTablet':
      return 'station'
    case 'waiterPhone':
      return screenForAWaiterPhone(route)
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
    case 'catalog':
    case 'review':
    case 'openItems':
    case 'orderOverview':
    case 'stations':
      return screenForTheDeviceSession(session, route.name)
    default:
      return assertNever(route)
  }
}

export type WaiterLanding =
  | { kind: 'lockedOrder' }
  | { kind: 'noFestival' }
  | { kind: 'noTables' }
  | { kind: 'areaList' }
  | { kind: 'plan'; areaId: string }

export interface WaiterLandingFacts {
  orderIsLocked: boolean
  festivalIsRunning: boolean
  areaIds: readonly string[]
  openedAreaId: string | null
}

export function anAreaMustBeChosen(areaCount: number): boolean {
  return areaCount > 1
}

export function waiterLandingFor(facts: WaiterLandingFacts): WaiterLanding {
  if (facts.orderIsLocked) {
    return { kind: 'lockedOrder' }
  }
  if (facts.areaIds.length === 0) {
    return facts.festivalIsRunning ? { kind: 'noTables' } : { kind: 'noFestival' }
  }
  if (!anAreaMustBeChosen(facts.areaIds.length)) {
    return { kind: 'plan', areaId: facts.areaIds[0] }
  }
  if (facts.openedAreaId !== null && facts.areaIds.includes(facts.openedAreaId)) {
    return { kind: 'plan', areaId: facts.openedAreaId }
  }
  return { kind: 'areaList' }
}

export interface AreaOfThePlan {
  areaId: string
  tables: readonly { tableId: string }[]
}

export function areaToOpenForTheTable(areas: readonly AreaOfThePlan[], tableId: string): string | null {
  if (!anAreaMustBeChosen(areas.length)) {
    return null
  }
  return areas.find((area) => area.tables.some((table) => table.tableId === tableId))?.areaId ?? null
}
