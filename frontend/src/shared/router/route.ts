import { assertNever } from '../core/assertNever'

export type AdminSection = 'festivals' | 'overview' | 'stations' | 'items' | 'ingredients' | 'staff'

export type AppRoute =
  | { name: 'enrolQr'; code: string }
  | { name: 'home' }
  | { name: 'catalog' }
  | { name: 'review' }
  | { name: 'openItems' }
  | { name: 'orderOverview' }
  | { name: 'stations' }
  | { name: 'admin'; section: AdminSection; festivalId: string | null; areaId?: string }

const ADMIN_SECTIONS: AdminSection[] = ['overview', 'festivals', 'stations', 'items', 'ingredients', 'staff']

function adminSectionFrom(segment: string | undefined): AdminSection {
  const wanted = (segment ?? '').toLowerCase()
  const match = ADMIN_SECTIONS.find((section) => section === wanted)
  return match ?? 'festivals'
}

function adminRouteFrom(segments: string[]): AppRoute {
  const section = adminSectionFrom(segments[1])
  const festivalId = section === 'festivals' ? (segments[2] ?? null) : null
  const areaId = segments[4]
  if (festivalId !== null && (segments[3] ?? '').toLowerCase() === 'areas' && areaId !== undefined) {
    return { name: 'admin', section, festivalId, areaId }
  }
  return { name: 'admin', section, festivalId }
}

export function resolveRoute(path: string): AppRoute {
  const segments = path
    .split('?')[0]
    .split('#')[0]
    .split('/')
    .filter((segment) => segment.length > 0)
  const first = (segments[0] ?? '').toLowerCase()
  if (segments.length === 0) {
    return { name: 'home' }
  }
  if (first === 'j' && segments.length >= 2) {
    return { name: 'enrolQr', code: segments[1] }
  }
  if (first === 'catalog') {
    return { name: 'catalog' }
  }
  if (first === 'review') {
    return { name: 'review' }
  }
  if (first === 'open-items') {
    return { name: 'openItems' }
  }
  if (first === 'order-overview') {
    return { name: 'orderOverview' }
  }
  if (first === 'stations') {
    return { name: 'stations' }
  }
  if (first === 'admin') {
    return adminRouteFrom(segments)
  }
  return { name: 'home' }
}

const THE_FESTIVAL_LIST: AppRoute = { name: 'admin', section: 'festivals', festivalId: null }

function parentOfAnAdminRoute(section: AdminSection, festivalId: string | null, areaId: string | undefined): AppRoute | null {
  if (festivalId !== null && areaId !== undefined) {
    return { name: 'admin', section: 'festivals', festivalId }
  }
  if (section === 'festivals' && festivalId === null) {
    return null
  }
  return THE_FESTIVAL_LIST
}

export function parentRouteOf(route: AppRoute): AppRoute | null {
  switch (route.name) {
    case 'home':
    case 'stations':
    case 'enrolQr':
      return null
    case 'catalog':
    case 'openItems':
    case 'orderOverview':
      return { name: 'home' }
    case 'review':
      return { name: 'catalog' }
    case 'admin':
      return parentOfAnAdminRoute(route.section, route.festivalId, route.areaId)
    default:
      return assertNever(route)
  }
}

function routeKey(route: AppRoute): string {
  switch (route.name) {
    case 'enrolQr':
      return `enrolQr/${route.code}`
    case 'admin':
      return `admin/${route.section}/${route.festivalId ?? ''}/${route.areaId ?? ''}`
    case 'home':
    case 'catalog':
    case 'review':
    case 'openItems':
    case 'orderOverview':
    case 'stations':
      return route.name
    default:
      return assertNever(route)
  }
}

export function isSameRoute(first: AppRoute, second: AppRoute): boolean {
  return routeKey(first) === routeKey(second)
}

export function routeToStartAt(path: string): AppRoute {
  const route = resolveRoute(path)
  switch (route.name) {
    case 'catalog':
    case 'review':
    case 'openItems':
    case 'orderOverview':
      return { name: 'home' }
    case 'home':
    case 'stations':
    case 'enrolQr':
    case 'admin':
      return route
    default:
      return assertNever(route)
  }
}

export { ADMIN_SECTIONS }
