import { isSameRoute, parentRouteOf, type AppRoute } from './route'

type StepKind = 'level' | 'dialog'

export interface BackStep {
  name: string
  value: string
  kind: StepKind
}

export interface Place {
  path: string
  route: AppRoute
  steps: readonly BackStep[]
}

export type NavigationReason = 'tap' | 'redirect'

export interface UserActivationState {
  isActive: boolean
}

interface TapFacts {
  reason: NavigationReason
  userActivation: UserActivationState | undefined
  movedThroughTheHistorySinceTheLastTap: boolean
}

interface HistoryFacts {
  entries: readonly Place[]
  index: number
  currentEntryMinusClosedSteps: Place
  wanted: Place
}

export type HistoryMove =
  | { kind: 'stay' }
  | { kind: 'replace' }
  | { kind: 'add' }
  | { kind: 'goBack'; entries: number }
  | { kind: 'goBackAndReplace'; entries: number }

function isSameStep(first: BackStep, second: BackStep): boolean {
  return first.name === second.name && first.value === second.value
}

function haveTheSameSteps(first: readonly BackStep[], second: readonly BackStep[]): boolean {
  return first.length === second.length && first.every((step, position) => isSameStep(step, second[position]))
}

export function holdsTheStep(steps: readonly BackStep[], wanted: BackStep): boolean {
  return steps.some((step) => isSameStep(step, wanted))
}

export function isSamePlace(first: Place, second: Place): boolean {
  return isSameRoute(first.route, second.route) && haveTheSameSteps(first.steps, second.steps)
}

function isParentPlace(parent: Place, child: Place): boolean {
  if (child.steps.length > 0) {
    return isSameRoute(parent.route, child.route) && haveTheSameSteps(parent.steps, child.steps.slice(0, -1))
  }
  const parentRoute = parentRouteOf(child.route)
  return parentRoute !== null && isSameRoute(parent.route, parentRoute)
}

export function withStep(place: Place, step: BackStep): Place {
  const existing = place.steps.findIndex((open) => open.name === step.name)
  const kept = existing === -1 ? place.steps : place.steps.slice(0, existing)
  return { ...place, steps: [...kept, step] }
}

export function withoutStep(place: Place, name: string): Place {
  const existing = place.steps.findIndex((open) => open.name === name)
  return existing === -1 ? place : { ...place, steps: place.steps.slice(0, existing) }
}

function levelsOf(steps: readonly BackStep[]): BackStep[] {
  const firstDialog = steps.findIndex((step) => step.kind === 'dialog')
  return firstDialog === -1 ? [...steps] : steps.slice(0, firstDialog)
}

export function placeToOpen(path: string, route: AppRoute, shown: Place, entries: readonly Place[], index: number): Place {
  if (isSameRoute(route, shown.route)) {
    return { path, route, steps: [] }
  }
  for (let candidate = index; candidate >= 0; candidate -= 1) {
    if (isSameRoute(entries[candidate].route, route)) {
      return { path, route, steps: levelsOf(entries[candidate].steps) }
    }
  }
  return { path, route, steps: [] }
}

export function aTapMayAddAnEntry(facts: TapFacts): boolean {
  return (
    facts.reason === 'tap'
    && facts.userActivation !== undefined
    && facts.userActivation.isActive
    && !facts.movedThroughTheHistorySinceTheLastTap
  )
}

function positionAfterTheClosedSteps(facts: HistoryFacts): number {
  if (isSamePlace(facts.currentEntryMinusClosedSteps, facts.entries[facts.index])) {
    return facts.index
  }
  for (let candidate = facts.index - 1; candidate >= 0; candidate -= 1) {
    if (isSamePlace(facts.entries[candidate], facts.currentEntryMinusClosedSteps)) {
      return candidate
    }
  }
  return facts.index
}

function moveOnFrom(parent: number, index: number, mayAddAnEntry: boolean): HistoryMove {
  if (parent === index) {
    return mayAddAnEntry ? { kind: 'add' } : { kind: 'replace' }
  }
  const landing = parent + 1
  return landing === index ? { kind: 'replace' } : { kind: 'goBackAndReplace', entries: index - landing }
}

export function historyMoveFor(facts: HistoryFacts, mayAddAnEntry: boolean): HistoryMove {
  const { entries, index, currentEntryMinusClosedSteps, wanted } = facts
  if (isSamePlace(wanted, entries[index])) {
    return { kind: 'stay' }
  }
  const placeAt = (position: number): Place => (position === index ? currentEntryMinusClosedSteps : entries[position])
  const position = positionAfterTheClosedSteps(facts)
  for (let candidate = position; candidate >= 0; candidate -= 1) {
    if (isSamePlace(placeAt(candidate), wanted)) {
      return candidate === index ? { kind: 'replace' } : { kind: 'goBack', entries: index - candidate }
    }
  }
  for (let candidate = position; candidate >= 0; candidate -= 1) {
    if (isParentPlace(placeAt(candidate), wanted)) {
      return moveOnFrom(candidate, index, mayAddAnEntry)
    }
  }
  return { kind: 'replace' }
}
