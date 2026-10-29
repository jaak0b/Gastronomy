import { describe, expect, it } from 'vitest'
import {
  aTapMayAddAnEntry,
  historyMoveFor,
  placeToOpen,
  withStep,
  withoutStep,
  type BackStep,
  type Place,
} from '../../../src/shared/router/historyMoves'
import type { AppRoute } from '../../../src/shared/router/route'

const HALLE: BackStep = { name: 'area', value: 'area-halle', kind: 'level' }
const GARTEN: BackStep = { name: 'area', value: 'area-garten', kind: 'level' }
const TISCH_12: BackStep = { name: 'table', value: 'table-12', kind: 'dialog' }
const SETTINGS: BackStep = { name: 'dialog-1', value: 'dialog-1', kind: 'dialog' }

const FESTIVAL_LIST: AppRoute = { name: 'admin', section: 'festivals', festivalId: null }
const SOMMERFEST: AppRoute = { name: 'admin', section: 'festivals', festivalId: 'fest-sommer' }
const SOMMERFEST_HALLE: AppRoute = { name: 'admin', section: 'festivals', festivalId: 'fest-sommer', areaId: 'area-halle' }
const ARTIKEL: AppRoute = { name: 'admin', section: 'items', festivalId: null }
const ZUTATEN: AppRoute = { name: 'admin', section: 'ingredients', festivalId: null }

function at(route: AppRoute, ...steps: BackStep[]): Place {
  return { path: '/', route, steps }
}

const AREA_LIST = at({ name: 'home' })
const PLAN_OF_HALLE = at({ name: 'home' }, HALLE)
const SHEET_OF_TISCH_12 = at({ name: 'home' }, HALLE, TISCH_12)
const ORDER_SCREEN = at({ name: 'catalog' })
const SUMMARY = at({ name: 'review' })
const SETTLE_VIEW = at({ name: 'openItems' })

describe('a tap that leads one level deeper', () => {
  it('adds an entry for a table opened on the plan of an area', () => {
    const move = historyMoveFor(
      { entries: [AREA_LIST, PLAN_OF_HALLE], index: 1, currentEntryMinusClosedSteps: PLAN_OF_HALLE, wanted: SHEET_OF_TISCH_12 },
      true,
    )

    expect(move).toEqual({ kind: 'add' })
  })

  it('adds an entry for the order screen opened from the sheet of a table, so back returns to the sheet', () => {
    const move = historyMoveFor(
      { entries: [AREA_LIST, PLAN_OF_HALLE, SHEET_OF_TISCH_12], index: 2, currentEntryMinusClosedSteps: SHEET_OF_TISCH_12, wanted: ORDER_SCREEN },
      true,
    )

    expect(move).toEqual({ kind: 'add' })
  })

  it('adds an entry for a festival opened from the festival list', () => {
    const move = historyMoveFor(
      { entries: [at(FESTIVAL_LIST)], index: 0, currentEntryMinusClosedSteps: at(FESTIVAL_LIST), wanted: at(SOMMERFEST) },
      true,
    )

    expect(move).toEqual({ kind: 'add' })
  })

  it('replaces the entry when the browser would not count the tap', () => {
    const move = historyMoveFor(
      { entries: [AREA_LIST, PLAN_OF_HALLE], index: 1, currentEntryMinusClosedSteps: PLAN_OF_HALLE, wanted: SHEET_OF_TISCH_12 },
      false,
    )

    expect(move).toEqual({ kind: 'replace' })
  })
})

describe('a move between siblings', () => {
  it('replaces the entry of the admin tab it leaves', () => {
    const move = historyMoveFor(
      { entries: [at(FESTIVAL_LIST), at(ARTIKEL)], index: 1, currentEntryMinusClosedSteps: at(ARTIKEL), wanted: at(ZUTATEN) },
      true,
    )

    expect(move).toEqual({ kind: 'replace' })
  })

  it('goes back to the entry above the festival list and replaces it when a tab is opened from the editor of an area', () => {
    const move = historyMoveFor(
      {
        entries: [at(FESTIVAL_LIST), at(SOMMERFEST), at(SOMMERFEST_HALLE)],
        index: 2,
        currentEntryMinusClosedSteps: at(SOMMERFEST_HALLE),
        wanted: at(ARTIKEL),
      },
      true,
    )

    expect(move).toEqual({ kind: 'goBackAndReplace', entries: 1 })
  })
})

describe('a move up to a screen the history already holds', () => {
  it('goes back to the plan of the area, past the sheet of the table, when the order screen leads up to the plan', () => {
    const move = historyMoveFor(
      {
        entries: [AREA_LIST, PLAN_OF_HALLE, SHEET_OF_TISCH_12, ORDER_SCREEN],
        index: 3,
        currentEntryMinusClosedSteps: ORDER_SCREEN,
        wanted: PLAN_OF_HALLE,
      },
      true,
    )

    expect(move).toEqual({ kind: 'goBack', entries: 2 })
  })

  it('goes back to the area list after sending, so no entry of the sent order is left', () => {
    const move = historyMoveFor(
      {
        entries: [AREA_LIST, PLAN_OF_HALLE, SHEET_OF_TISCH_12, ORDER_SCREEN, SUMMARY],
        index: 4,
        currentEntryMinusClosedSteps: SUMMARY,
        wanted: AREA_LIST,
      },
      false,
    )

    expect(move).toEqual({ kind: 'goBack', entries: 4 })
  })

  it('replaces the order screen with the settle view after sending, so back from it reaches the sheet of the table', () => {
    const move = historyMoveFor(
      {
        entries: [AREA_LIST, PLAN_OF_HALLE, SHEET_OF_TISCH_12, ORDER_SCREEN, SUMMARY],
        index: 4,
        currentEntryMinusClosedSteps: SUMMARY,
        wanted: SETTLE_VIEW,
      },
      false,
    )

    expect(move).toEqual({ kind: 'goBackAndReplace', entries: 1 })
  })
})

describe('a redirect', () => {
  it('replaces the entry of a screen whose parent is not in the history', () => {
    const move = historyMoveFor({ entries: [AREA_LIST], index: 0, currentEntryMinusClosedSteps: AREA_LIST, wanted: SUMMARY }, false)

    expect(move).toEqual({ kind: 'replace' })
  })

  it('never adds an entry, even one level deeper', () => {
    const mayAdd = aTapMayAddAnEntry({
      reason: 'redirect',
      userActivation: { isActive: true },
      movedThroughTheHistorySinceTheLastTap: false,
    })

    expect(mayAdd).toBe(false)
  })
})

describe('a dialog', () => {
  it('goes back to the entry below when it is closed by its own button', () => {
    const move = historyMoveFor(
      { entries: [PLAN_OF_HALLE, at({ name: 'home' }, HALLE, SETTINGS)], index: 1, currentEntryMinusClosedSteps: PLAN_OF_HALLE, wanted: PLAN_OF_HALLE },
      true,
    )

    expect(move).toEqual({ kind: 'goBack', entries: 1 })
  })

  it('replaces its entry when it was opened without an entry of its own and is closed again', () => {
    const move = historyMoveFor(
      { entries: [at({ name: 'home' }, HALLE, SETTINGS)], index: 0, currentEntryMinusClosedSteps: PLAN_OF_HALLE, wanted: PLAN_OF_HALLE },
      true,
    )

    expect(move).toEqual({ kind: 'replace' })
  })

  it('gives its entry to the screen it leads to, so back from that screen skips the closed dialog', () => {
    const move = historyMoveFor(
      {
        entries: [AREA_LIST, PLAN_OF_HALLE, at({ name: 'home' }, HALLE, SETTINGS)],
        index: 2,
        currentEntryMinusClosedSteps: PLAN_OF_HALLE,
        wanted: ORDER_SCREEN,
      },
      true,
    )

    expect(move).toEqual({ kind: 'replace' })
  })
})

describe('a move that changes nothing', () => {
  it('leaves the history alone', () => {
    const move = historyMoveFor(
      { entries: [AREA_LIST, PLAN_OF_HALLE], index: 1, currentEntryMinusClosedSteps: PLAN_OF_HALLE, wanted: PLAN_OF_HALLE },
      true,
    )

    expect(move).toEqual({ kind: 'stay' })
  })
})

describe('whether a tap may add an entry', () => {
  it('may while the tap is active and nothing moved through the history since', () => {
    const mayAdd = aTapMayAddAnEntry({
      reason: 'tap',
      userActivation: { isActive: true },
      movedThroughTheHistorySinceTheLastTap: false,
    })

    expect(mayAdd).toBe(true)
  })

  it('may not once the activation of the tap has passed', () => {
    const mayAdd = aTapMayAddAnEntry({
      reason: 'tap',
      userActivation: { isActive: false },
      movedThroughTheHistorySinceTheLastTap: false,
    })

    expect(mayAdd).toBe(false)
  })

  it('may not in a browser without the user activation API', () => {
    const mayAdd = aTapMayAddAnEntry({
      reason: 'tap',
      userActivation: undefined,
      movedThroughTheHistorySinceTheLastTap: false,
    })

    expect(mayAdd).toBe(false)
  })

  it('may not after a back press until the next tap, because the browser no longer counts the earlier one', () => {
    const mayAdd = aTapMayAddAnEntry({
      reason: 'tap',
      userActivation: { isActive: true },
      movedThroughTheHistorySinceTheLastTap: true,
    })

    expect(mayAdd).toBe(false)
  })
})

describe('the place a move to a screen opens', () => {
  it('keeps the open area of the plan the history returns to, without the sheet of the table', () => {
    const place = placeToOpen(
      '/',
      { name: 'home' },
      ORDER_SCREEN,
      [AREA_LIST, PLAN_OF_HALLE, SHEET_OF_TISCH_12, ORDER_SCREEN],
      3,
    )

    expect(place.steps).toEqual([HALLE])
  })

  it('opens a screen the history does not hold without any step', () => {
    const place = placeToOpen('/catalog', { name: 'catalog' }, SHEET_OF_TISCH_12, [AREA_LIST, PLAN_OF_HALLE, SHEET_OF_TISCH_12], 2)

    expect(place.steps).toEqual([])
  })

  it('opens the screen on display again without its steps', () => {
    const place = placeToOpen('/', { name: 'home' }, PLAN_OF_HALLE, [AREA_LIST, PLAN_OF_HALLE], 1)

    expect(place.steps).toEqual([])
  })
})

describe('opening a step that is already open', () => {
  it('replaces it with the new one and closes what was open above it', () => {
    const place = withStep(at({ name: 'home' }, HALLE, TISCH_12), GARTEN)

    expect(place.steps).toEqual([GARTEN])
  })
})

describe('closing a step', () => {
  const CONTEXT_MENU: BackStep = { name: 'dialog-2', value: 'dialog-2', kind: 'dialog' }
  const TABLE_DIALOG: BackStep = { name: 'dialog-3', value: 'dialog-3', kind: 'dialog' }

  it('leaves a dialog that opened above a closing menu in the same move', () => {
    const place = withoutStep(at(FESTIVAL_LIST, CONTEXT_MENU, TABLE_DIALOG), 'dialog-2')

    expect(place.steps).toEqual([TABLE_DIALOG])
  })

  it('closes what was open on a level that closes', () => {
    const place = withoutStep(at({ name: 'home' }, HALLE, TISCH_12), 'area')

    expect(place.steps).toEqual([])
  })
})
