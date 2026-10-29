import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type Konva from 'konva'
import { createVuetify } from 'vuetify'
import { navigate } from '../../../src/shared/router/router'
import { appTheme } from '../../../src/shared/theme'
import { isOnScreen, waitUntil } from '../../support/dom'
import {
  centreOf,
  colourAt,
  dragBy,
  layOutTheScreen,
  movePointerTo,
  pressAt,
  releaseOnTheDrawingAt,
  shapesNamed,
  tapAt,
} from '../../support/konvaDrawing'
import { aGeometry, aPlanArea, aPlanStationShape, aPlanTable } from '../../support/wireViews'
import {
  HALL,
  TABLE_ACTIONS,
  TISCH_12,
  mountThePlan,
  planLaptop,
  tableNode,
  tapTheTable,
  theDrawing,
} from './tablePlanFixture'

enableAutoUnmount(afterEach)

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  navigate('/')
})

afterEach(() => {
  vi.restoreAllMocks()
  vi.unstubAllGlobals()
})

function areaWithOneTable(handOutProgress: 'none' | 'some' | 'all', settlementProgress: 'none' | 'some' | 'all') {
  return aPlanArea({ tables: [aPlanTable({ ...TISCH_12, handOutProgress, settlementProgress })] })
}

describe('the plan of one area on a waiter\'s phone', () => {
  it('draws each table at its stored place and size, rotation included', async () => {
    const turned = aPlanTable({ tableId: 'table-7', name: '7', geometry: aGeometry({ x: 150, y: 120, width: 60, height: 30, rotation: 45 }) })
    planLaptop([aPlanArea({ tables: [turned] })])

    await mountThePlan()

    const node = tableNode('table-7')
    expect({ x: node.x(), y: node.y(), width: node.width(), height: node.height(), rotation: node.rotation() }).toEqual({
      x: 150,
      y: 120,
      width: 60,
      height: 30,
      rotation: 45,
    })
  })

  it('writes the name of each table inside it', async () => {
    planLaptop([HALL])

    await mountThePlan()

    expect(tableNode('table-12').findOne<Konva.Text>('Text')?.text()).toBe('12')
  })

  it('keeps a name that is wider than its table in one piece', async () => {
    const narrow = aPlanTable({ tableId: 'table-9', name: 'Abcd', geometry: aGeometry({ x: 20, y: 30, width: 30, height: 60 }) })
    planLaptop([aPlanArea({ tables: [narrow] })])

    await mountThePlan()

    expect(tableNode('table-9').findOne<Konva.Text>('Text')?.textArr.map((line) => line.text)).toEqual(['Abcd'])
  })

  it('draws the stations with their names', async () => {
    planLaptop([HALL])

    await mountThePlan()

    expect(shapesNamed(theDrawing(), 'plan-station').map((node) => (node as Konva.Group).findOne<Konva.Text>('Text')?.text())).toEqual([
      'Schank',
    ])
  })

  it('keeps a station name that is wider than its shape in one piece, as it does for a table', async () => {
    const narrow = aPlanStationShape({ stationName: 'Abcd', geometry: aGeometry({ x: 150, y: 30, width: 30, height: 60 }) })
    planLaptop([aPlanArea({ tables: [TISCH_12], stationShapes: [narrow] })])

    await mountThePlan()

    const station = shapesNamed(theDrawing(), 'plan-station')[0] as Konva.Group
    expect(station.findOne<Konva.Text>('Text')?.textArr.map((line) => line.text)).toEqual(['Abcd'])
  })

  it('draws a station name of two words whole in a station too low for both lines at the largest size', async () => {
    const low = aPlanStationShape({ stationName: 'Bar Nord', geometry: aGeometry({ x: 150, y: 30, width: 50, height: 30 }) })
    planLaptop([aPlanArea({ tables: [TISCH_12], stationShapes: [low] })])

    await mountThePlan()

    const station = shapesNamed(theDrawing(), 'plan-station')[0] as Konva.Group
    expect(station.findOne<Konva.Text>('Text')?.textArr.map((line) => line.text)).toEqual(['Bar', 'Nord'])
  })

  it('opens nothing when the waiter taps a station', async () => {
    planLaptop([HALL])
    await mountThePlan()

    await tapAt(theDrawing(), centreOf(shapesNamed(theDrawing(), 'plan-station')[0]))

    expect(isOnScreen(TABLE_ACTIONS)).toBe(false)
  })

  it('starts with the drawing fitted to the width of the screen', async () => {
    layOutTheScreen({ width: 200, height: 500 })
    planLaptop([HALL])

    await mountThePlan()

    expect(theDrawing().scaleX()).toBe(0.5)
  })
})

describe('the colours of a table on the plan', () => {
  it.each([
    ['none', '#efcaca'],
    ['some', '#dbceb3'],
    ['all', '#c0dccd'],
  ] as const)('fills a table whose hand-out state is %s with the matching status tint', async (state, fill) => {
    planLaptop([areaWithOneTable(state, 'all')])

    await mountThePlan()

    expect(tableNode('table-12').findOne<Konva.Rect>('Rect')?.fill()).toBe(fill)
  })

  it.each([
    ['none', '#c94f4f'],
    ['some', '#875a00'],
    ['all', '#2e8b57'],
  ] as const)('borders a table whose settlement state is %s in the matching status colour', async (state, stroke) => {
    planLaptop([areaWithOneTable('all', state)])

    await mountThePlan()

    expect(tableNode('table-12').findOne<Konva.Rect>('Rect')?.stroke()).toBe(stroke)
  })

  it('draws the colours of the dark theme when the phone switches to it', async () => {
    planLaptop([areaWithOneTable('none', 'some')])
    const vuetify = createVuetify({ theme: appTheme })
    await mountThePlan('de', [vuetify])

    await vuetify.theme.change('dark')

    await waitUntil(() =>
      expect({
        fill: tableNode('table-12').findOne<Konva.Rect>('Rect')?.fill(),
        stroke: tableNode('table-12').findOne<Konva.Rect>('Rect')?.stroke(),
      }).toEqual({ fill: '#5d3e43', stroke: '#ffd54f' }),
    )
  })
})

const AMBER = [135, 90, 0, 255]
const GREEN = [46, 139, 87, 255]
const RED = [201, 79, 79, 255]
const GREEN_TINT = [192, 220, 205, 255]

describe('the name of a table on the plan', () => {
  it('keeps a gap to the frame even when it needs the whole width of the table', async () => {
    layOutTheScreen({ width: 800, height: 400 })
    const narrow = aPlanTable({ tableId: 'table-9', name: 'H12', geometry: aGeometry({ x: 20, y: 30, width: 32, height: 40 }) })
    planLaptop([aPlanArea({ tables: [narrow] })])

    await mountThePlan()

    expect({
      firstColumnInsideTheFrame: colourAt(theDrawing(), { x: 48, y: 100 }),
      secondColumnInsideTheFrame: colourAt(theDrawing(), { x: 49, y: 100 }),
      thirdColumnInsideTheFrame: colourAt(theDrawing(), { x: 50, y: 100 }),
      fourthColumnInsideTheFrame: colourAt(theDrawing(), { x: 51, y: 100 }),
    }).toEqual({
      firstColumnInsideTheFrame: GREEN_TINT,
      secondColumnInsideTheFrame: GREEN_TINT,
      thirdColumnInsideTheFrame: GREEN_TINT,
      fourthColumnInsideTheFrame: GREEN_TINT,
    })
  })

  it('keeps both lines of a name in a table just high enough for them', async () => {
    const table = aPlanTable({ tableId: 'table-9', name: 'Tisch Mitte', geometry: aGeometry({ x: 20, y: 30, width: 56, height: 36 }) })
    planLaptop([aPlanArea({ tables: [table] })])

    await mountThePlan()

    expect(tableNode('table-9').findOne<Konva.Text>('Text')?.textArr.map((line) => line.text)).toEqual(['Tisch', 'Mitte'])
  })
})

describe('the frame of a table on the plan', () => {
  it('reaches nowhere outside the rectangle the plan gives the table', async () => {
    const table = aPlanTable({
      tableId: 'table-7',
      name: '7',
      geometry: aGeometry({ x: 150, y: 120, width: 60, height: 30 }),
    })
    planLaptop([aPlanArea({ tables: [table] })])

    await mountThePlan()

    expect(tableNode('table-7').getClientRect({ skipTransform: true })).toEqual({ x: 0, y: 0, width: 60, height: 30 })
  })

  it('stays visible beside the frame of the table that touches it on the right', async () => {
    const left = aPlanTable({
      tableId: 'table-1',
      name: '1',
      settlementProgress: 'some',
      geometry: aGeometry({ x: 20, y: 30, width: 80, height: 40 }),
    })
    const right = aPlanTable({
      tableId: 'table-2',
      name: '2',
      settlementProgress: 'all',
      geometry: aGeometry({ x: 100, y: 30, width: 80, height: 40 }),
    })
    planLaptop([aPlanArea({ tables: [left, right] })])

    await mountThePlan()

    expect({
      lastColumnOfTheLeftFrame: colourAt(theDrawing(), { x: 99, y: 50 }),
      firstColumnOfTheRightFrame: colourAt(theDrawing(), { x: 100, y: 50 }),
    }).toEqual({ lastColumnOfTheLeftFrame: AMBER, firstColumnOfTheRightFrame: GREEN })
  })

  it('stays visible beside the frame of the table that touches it below', async () => {
    const upper = aPlanTable({
      tableId: 'table-1',
      name: '1',
      settlementProgress: 'all',
      geometry: aGeometry({ x: 20, y: 30, width: 80, height: 40 }),
    })
    const lower = aPlanTable({
      tableId: 'table-14',
      name: '14',
      settlementProgress: 'none',
      geometry: aGeometry({ x: 20, y: 70, width: 80, height: 40 }),
    })
    planLaptop([aPlanArea({ tables: [upper, lower] })])

    await mountThePlan()

    expect({
      lastRowOfTheUpperFrame: colourAt(theDrawing(), { x: 60, y: 69 }),
      firstRowOfTheLowerFrame: colourAt(theDrawing(), { x: 60, y: 70 }),
    }).toEqual({ lastRowOfTheUpperFrame: GREEN, firstRowOfTheLowerFrame: RED })
  })
})

describe('telling a tap on a table from a pan of the plan', () => {
  it('opens the table the waiter taps', async () => {
    planLaptop([HALL])
    await mountThePlan()

    await tapTheTable('table-14')

    expect(document.querySelector('[data-test="table-actions-title"]')?.textContent?.trim()).toBe('Tisch 14')
  })

  it('opens no table when the waiter pans the plan by dragging it, and lets go over the same table', async () => {
    layOutTheScreen({ width: 400, height: 150 })
    planLaptop([HALL])
    await mountThePlan()
    const start = centreOf(tableNode('table-12'))

    await pressAt(theDrawing(), start)
    await movePointerTo({ x: start.x, y: start.y - 12 })
    await releaseOnTheDrawingAt(theDrawing(), { x: start.x, y: start.y - 12 })

    expect({ panned: theDrawing().y(), tableOpened: isOnScreen(TABLE_ACTIONS) }).toEqual({
      panned: -12,
      tableOpened: false,
    })
  })

  it('moves the plan with the finger', async () => {
    layOutTheScreen({ width: 400, height: 150 })
    planLaptop([HALL])
    await mountThePlan()

    await dragBy(theDrawing(), tableNode('table-12'), { x: 0, y: -60 })

    expect(theDrawing().y()).toBe(-60)
  })

  it('stops the plan at its top edge', async () => {
    layOutTheScreen({ width: 400, height: 150 })
    planLaptop([HALL])
    await mountThePlan()

    await dragBy(theDrawing(), tableNode('table-12'), { x: 30, y: 60 })

    expect({ x: theDrawing().x(), y: theDrawing().y() }).toEqual({ x: 0, y: 0 })
  })

  it('stops the plan where its lowest table reaches the bottom of the screen', async () => {
    layOutTheScreen({ width: 400, height: 150 })
    planLaptop([HALL])
    await mountThePlan()

    await dragBy(theDrawing(), tableNode('table-12'), { x: 0, y: -200 })

    expect(theDrawing().y()).toBe(-110)
  })
})
