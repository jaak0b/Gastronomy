import { nextTick, ref, shallowRef, type Ref } from 'vue'
import { assertNever } from '../core/assertNever'
import { currentAddress } from './currentAddress'
import { resolveRoute, routeToStartAt, type AppRoute } from './route'
import {
  noteRouteChangeForBackButtonTrap,
  prepareBackButtonTrapForFreshStart,
  startBackButtonTrap,
} from './backButtonTrap'
import {
  aTapMayAddAnEntry,
  historyMoveFor,
  holdsTheStep,
  isSamePlace,
  placeToOpen,
  withStep,
  withoutStep,
  type BackStep,
  type HistoryMove,
  type NavigationReason,
  type Place,
  type UserActivationState,
} from './historyMoves'

export { ADMIN_SECTIONS, parentRouteOf, resolveRoute } from './route'
export type { AdminSection, AppRoute } from './route'

export interface BackHandler {
  close: () => void
  refusesBack: () => boolean
}

interface EntryStamp {
  router: string
  entry: number
}

interface Traversal {
  entry: number
  replaceWith: Place | null
  abandonsTheEntriesAbove: boolean
}

const TAP_EVENTS = ['pointerdown', 'mousedown', 'touchend', 'keydown', 'click']

const THIS_ROUTER = crypto.getRandomValues(new Uint32Array(2)).join('-')

const startPlace: Place = {
  path: currentAddress(),
  route: routeToStartAt(window.location.pathname),
  steps: [],
}

export const currentRoute: Ref<AppRoute> = ref(startPlace.route)

const shownSteps = shallowRef<readonly BackStep[]>([])

let shownPath = startPlace.path
let entries: Place[] = [startPlace]
let index = 0
let currentEntryMinusClosedSteps: Place = startPlace
let traversal: Traversal | null = null
let historyIsQueued = false
let aRedirectIsQueued = false
let movedThroughTheHistorySinceTheLastTap = false
const backHandlers = new Map<string, BackHandler>()

function shownPlace(): Place {
  return { path: shownPath, route: currentRoute.value, steps: shownSteps.value }
}

function show(place: Place): void {
  shownPath = place.path
  currentRoute.value = place.route
  shownSteps.value = place.steps
}

function stampOf(entry: number): EntryStamp {
  return { router: THIS_ROUTER, entry }
}

function entryStampedByThisRouterIn(state: unknown): number | null {
  if (
    typeof state === 'object'
    && state !== null
    && 'router' in state
    && state.router === THIS_ROUTER
    && 'entry' in state
    && typeof state.entry === 'number'
  ) {
    return state.entry
  }
  return null
}

function userActivationOfThisBrowser(): UserActivationState | undefined {
  const browser: { userActivation?: UserActivationState } = window.navigator
  return browser.userActivation
}

function noteTheShownAddressForTheTrap(): void {
  noteRouteChangeForBackButtonTrap(currentRoute.value)
}

function replaceTheEntry(place: Place): void {
  entries = [...entries.slice(0, index), place]
  window.history.replaceState(stampOf(index), '', place.path)
  currentEntryMinusClosedSteps = place
}

function addAnEntry(place: Place): void {
  if (!isSamePlace(currentEntryMinusClosedSteps, entries[index])) {
    replaceTheEntry(currentEntryMinusClosedSteps)
  }
  entries = [...entries.slice(0, index + 1), place]
  index += 1
  window.history.pushState(stampOf(index), '', place.path)
  currentEntryMinusClosedSteps = place
}

function traverseTo(entry: number, replaceWith: Place | null, abandonsTheEntriesAbove: boolean): void {
  traversal = { entry, replaceWith, abandonsTheEntriesAbove }
  window.history.go(entry - index)
}

function applyTheMove(move: HistoryMove, wanted: Place): void {
  switch (move.kind) {
    case 'stay':
      currentEntryMinusClosedSteps = entries[index]
      return
    case 'replace':
      replaceTheEntry(wanted)
      return
    case 'add':
      addAnEntry(wanted)
      return
    case 'goBack':
      traverseTo(index - move.entries, null, true)
      return
    case 'goBackAndReplace':
      traverseTo(index - move.entries, wanted, true)
      return
    default:
      assertNever(move)
  }
}

function writeTheHistory(): void {
  historyIsQueued = false
  if (traversal !== null) {
    return
  }
  const wanted = shownPlace()
  const mayAddAnEntry = aTapMayAddAnEntry({
    reason: aRedirectIsQueued ? 'redirect' : 'tap',
    userActivation: userActivationOfThisBrowser(),
    movedThroughTheHistorySinceTheLastTap,
  })
  aRedirectIsQueued = false
  applyTheMove(historyMoveFor({ entries, index, currentEntryMinusClosedSteps, wanted }, mayAddAnEntry), wanted)
  if (traversal === null) {
    noteTheShownAddressForTheTrap()
  }
}

function queueTheHistory(reason: NavigationReason): void {
  if (reason === 'redirect') {
    aRedirectIsQueued = true
  }
  if (historyIsQueued) {
    return
  }
  historyIsQueued = true
  void nextTick(writeTheHistory)
}

function closeTheDialogsOf(steps: readonly BackStep[]): void {
  for (const step of steps) {
    const handler = backHandlers.get(step.name)
    backHandlers.delete(step.name)
    handler?.close()
  }
}

function aDialogRefusesToClose(steps: readonly BackStep[]): boolean {
  return steps.some((step) => backHandlers.get(step.name)?.refusesBack() === true)
}

function showTheEntryTheUserWentTo(place: Place, closing: readonly BackStep[]): void {
  currentEntryMinusClosedSteps = place
  show(place)
  noteTheShownAddressForTheTrap()
  closeTheDialogsOf(closing)
}

function finishTheTraversal(finished: Traversal): void {
  if (finished.replaceWith === null) {
    currentEntryMinusClosedSteps = entries[index]
  } else {
    replaceTheEntry(finished.replaceWith)
  }
  if (finished.abandonsTheEntriesAbove) {
    entries = entries.slice(0, index + 1)
  }
  writeTheHistory()
}

function handlePopState(event: PopStateEvent): void {
  const arrived = entryStampedByThisRouterIn(event.state)
  if (arrived === null) {
    return
  }
  movedThroughTheHistorySinceTheLastTap = true
  const ours = traversal
  const cameFrom = index
  traversal = null
  index = arrived
  if (ours !== null && ours.entry === arrived) {
    finishTheTraversal(ours)
    return
  }
  if (arrived >= entries.length) {
    traverseTo(cameFrom, null, false)
    return
  }
  const place = entries[arrived]
  const closing = shownSteps.value.filter((step) => !holdsTheStep(place.steps, step))
  if (aDialogRefusesToClose(closing)) {
    traverseTo(cameFrom, null, false)
    return
  }
  showTheEntryTheUserWentTo(place, closing)
}

function noteTheTap(): void {
  movedThroughTheHistorySinceTheLastTap = false
}

export function startRouter(): void {
  window.history.replaceState(stampOf(index), '')
  window.addEventListener('popstate', handlePopState)
  for (const type of TAP_EVENTS) {
    window.addEventListener(type, noteTheTap, { capture: true })
  }
  startBackButtonTrap()
}

export function navigate(path: string, reason: NavigationReason = 'tap'): void {
  show(placeToOpen(path, resolveRoute(path), shownPlace(), entries, index))
  queueTheHistory(reason)
}

export function openStep(step: BackStep, onBack?: BackHandler): void {
  shownSteps.value = withStep(shownPlace(), step).steps
  if (onBack !== undefined) {
    backHandlers.set(step.name, onBack)
  }
  queueTheHistory('tap')
}

export function closeStep(name: string): void {
  const shown = shownPlace()
  const stillShown = withoutStep(shown, name)
  const withoutTheClosedStep = withoutStep(currentEntryMinusClosedSteps, name)
  backHandlers.delete(name)
  if (stillShown === shown && withoutTheClosedStep === currentEntryMinusClosedSteps) {
    return
  }
  currentEntryMinusClosedSteps = withoutTheClosedStep
  shownSteps.value = stillShown.steps
  closeTheDialogsOf(shown.steps.filter((step) => step.name !== name && !holdsTheStep(stillShown.steps, step)))
  queueTheHistory('tap')
}

export function openStepValue(name: string): string | null {
  return shownSteps.value.find((step) => step.name === name)?.value ?? null
}

export function startOverAt(path: string): void {
  prepareBackButtonTrapForFreshStart(path)
  window.location.replace(path)
}
