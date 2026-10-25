import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount } from '@vue/test-utils'
import Konva from 'konva'
import { textOnScreen, waitUntil } from '../../../support/dom'
import {
  centreOf,
  dragBy,
  movePointerTo,
  pressAt,
  releaseOnTheDrawingAt,
  shapesNamed,
  shapeWithId,
  tapAt,
} from '../../../support/konvaDrawing'
import { answer, emptyAnswer, inTurn, refusal } from '../../../support/laptop'
import {
  HALL_WITH_TWO_TABLES,
  MOVE_PATH,
  PLAN_PATH,
  TABLE_A1_SHAPE_ID,
  TABLE_A2_SHAPE_ID,
  TENT,
  editorLaptop,
  mountEditor,
  shapesWithTheHandles,
  stationRow,
  tableRow,
  theDrawing,
} from './tablePlanEditorFixture'
import { aGeometry, anAdminTable } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

function twoTables() {
  return [HALL_WITH_TWO_TABLES, TENT]
}

async function tapTheShape(id: string, modifiers: { ctrlKey?: boolean; shiftKey?: boolean } = {}) {
  const stage = theDrawing()
  await tapAt(stage, centreOf(shapeWithId(stage, id)), modifiers)
}

async function dragTheMarquee(from: { x: number; y: number }, to: { x: number; y: number }) {
  const stage = theDrawing()
  await pressAt(stage, from)
  await movePointerTo({ x: from.x + 10, y: from.y + 10 })
  await movePointerTo(to)
  await releaseOnTheDrawingAt(stage, to)
}

describe('selecting several tables', () => {
  it('adds a table with a ctrl click and shows no resize handles for more than one', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()

    await tapTheShape(TABLE_A1_SHAPE_ID)
    await tapTheShape(TABLE_A2_SHAPE_ID, { ctrlKey: true })

    expect([
      tableRow('table-a1').classList.contains('selected-row'),
      tableRow('table-a2').classList.contains('selected-row'),
      shapesWithTheHandles(),
    ]).toEqual([true, true, []])
  })

  it('adds a table with a shift click as well', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()

    await tapTheShape(TABLE_A1_SHAPE_ID)
    await tapTheShape(TABLE_A2_SHAPE_ID, { shiftKey: true })

    expect(tableRow('table-a2').classList.contains('selected-row')).toBe(true)
  })

  it('leaves only the table under a plain click selected', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()
    await tapTheShape(TABLE_A1_SHAPE_ID)
    await tapTheShape(TABLE_A2_SHAPE_ID, { ctrlKey: true })

    await tapTheShape(TABLE_A2_SHAPE_ID)

    expect([
      tableRow('table-a1').classList.contains('selected-row'),
      tableRow('table-a2').classList.contains('selected-row'),
      shapesWithTheHandles(),
    ]).toEqual([false, true, [TABLE_A2_SHAPE_ID]])
  })

  it('puts the resize handles on the one table of a selection with one table', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()

    await tapTheShape(TABLE_A2_SHAPE_ID)

    expect(shapesWithTheHandles()).toEqual([TABLE_A2_SHAPE_ID])
  })
})

describe('a marquee on an empty spot', () => {
  it('selects every table its box touches', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()

    await dragTheMarquee({ x: 6, y: 6 }, { x: 456, y: 306 })

    expect([
      tableRow('table-a1').classList.contains('selected-row'),
      tableRow('table-a2').classList.contains('selected-row'),
      stationRow('station-schank').classList.contains('selected-row'),
    ]).toEqual([true, true, false])
  })

  it('draws a dashed box with a translucent fill while the pointer moves', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()
    const stage = theDrawing()
    await pressAt(stage, { x: 6, y: 6 })

    await movePointerTo({ x: 306, y: 156 })

    const box = shapesNamed(stage, 'marquee')[0] as Konva.Rect
    const fill = shapesNamed(stage, 'marquee-fill')[0] as Konva.Rect
    expect([box.dash(), box.stroke(), box.listening()]).toEqual([[4, 4], '#1f2937', false])
    expect([fill.fill(), fill.opacity(), fill.listening()]).toEqual(['#1f2937', 0.12, false])
    await releaseOnTheDrawingAt(stage, { x: 306, y: 156 })
    expect(shapesNamed(stage, 'marquee')).toEqual([])
  })

  it('replaces the selection of a plain marquee', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()
    await tapTheShape(TABLE_A1_SHAPE_ID)

    await dragTheMarquee({ x: 6, y: 126 }, { x: 156, y: 216 })

    expect([
      tableRow('table-a1').classList.contains('selected-row'),
      tableRow('table-a2').classList.contains('selected-row'),
    ]).toEqual([false, true])
  })

  it('adds to the selection of a ctrl marquee', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()
    await tapTheShape(TABLE_A1_SHAPE_ID)
    const stage = theDrawing()
    await pressAt(stage, { x: 6, y: 126 }, { ctrlKey: true })

    await movePointerTo({ x: 156, y: 216 })
    await releaseOnTheDrawingAt(stage, { x: 156, y: 216 })

    expect([
      tableRow('table-a1').classList.contains('selected-row'),
      tableRow('table-a2').classList.contains('selected-row'),
    ]).toEqual([true, true])
  })

  it('leaves a short press on an empty spot a click that clears the selection', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()
    await tapTheShape(TABLE_A1_SHAPE_ID)

    await tapAt(theDrawing(), { x: 525, y: 375 })

    await waitUntil(() => expect(tableRow('table-a1').classList.contains('selected-row')).toBe(false))
  })
})

describe('dragging a group of tables', () => {
  it('snaps the dragged table past its selected sibling and sends one move request and one reload', async () => {
    const laptop = editorLaptop({ areas: twoTables() })
    await mountEditor()
    await tapTheShape(TABLE_A1_SHAPE_ID)
    await tapTheShape(TABLE_A2_SHAPE_ID, { ctrlKey: true })
    laptop.forgetCalls()

    await dragBy(theDrawing(), shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID), { x: 127.5, y: 0 })

    await waitUntil(() => expect(laptop.callsTo('GET', PLAN_PATH)).toHaveLength(1))
    expect(laptop.callsTo('POST', MOVE_PATH)).toHaveLength(1)
    expect(laptop.writtenBodies()).toEqual([
      {
        tables: [
          { tableId: 'table-a1', geometry: { x: 110, y: 30, width: 80, height: 40, rotation: 0 } },
          { tableId: 'table-a2', geometry: { x: 110, y: 100, width: 80, height: 40, rotation: 0 } },
        ],
      },
    ])
  })

  it("shows the tables where the laptop's answer puts them after the reload", async () => {
    const movedPlan = {
      ...HALL_WITH_TWO_TABLES,
      tables: [
        anAdminTable({
          tableId: 'table-a1',
          name: 'A1',
          geometry: aGeometry({ x: 210, y: 220, width: 80, height: 40 }),
        }),
        anAdminTable({
          tableId: 'table-a2',
          name: 'A2',
          geometry: aGeometry({ x: 210, y: 290, width: 80, height: 40 }),
        }),
      ],
    }
    const laptop = editorLaptop({
      areas: twoTables(),
      planReply: inTurn(
        answer({ areas: twoTables() }),
        answer({ areas: [movedPlan, TENT] }),
      ),
      writeReply: emptyAnswer(204),
    })
    await mountEditor()
    await tapTheShape(TABLE_A1_SHAPE_ID)
    await tapTheShape(TABLE_A2_SHAPE_ID, { ctrlKey: true })
    laptop.forgetCalls()

    await dragBy(theDrawing(), shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID), { x: 75, y: 150 })

    await waitUntil(() => expect(shapeWithId(theDrawing(), TABLE_A2_SHAPE_ID).x()).toBe(210))
    const first = shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)
    const second = shapeWithId(theDrawing(), TABLE_A2_SHAPE_ID)
    expect([first.x(), first.y(), second.x(), second.y()]).toEqual([210, 220, 210, 290])
    expect(laptop.callsTo('POST', MOVE_PATH)).toHaveLength(1)
    expect(laptop.callsTo('GET', PLAN_PATH)).toHaveLength(1)
  })

  it('leaves both tables where they started when the laptop refuses the group move', async () => {
    const laptop = editorLaptop({
      areas: twoTables(),
      writeReply: refusal('errors.admin.actionFailed', { status: 400 }),
    })
    await mountEditor()
    await tapTheShape(TABLE_A1_SHAPE_ID)
    await tapTheShape(TABLE_A2_SHAPE_ID, { ctrlKey: true })
    const first = shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)
    const second = shapeWithId(theDrawing(), TABLE_A2_SHAPE_ID)

    await dragBy(theDrawing(), first, { x: 75, y: 150 })

    expect(laptop.writes()).toHaveLength(1)
    expect(laptop.writes()[0].url).toBe(MOVE_PATH)
    expect([first.x(), first.y(), second.x(), second.y()]).toEqual([20, 30, 20, 100])
    expect(textOnScreen('[data-test="table-plan-drawing-column"] [data-test="refusal"]').trim()).toBe(
      'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
    )
  })
})
