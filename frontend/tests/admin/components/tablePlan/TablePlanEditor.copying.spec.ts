import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { isOnScreen, onScreen, textOnScreen, waitUntil } from '../../../support/dom'
import {
  centreOf,
  rightClickAt,
  shapeWithId,
  tapAt,
} from '../../../support/konvaDrawing'
import { answer, inTurn, refusal } from '../../../support/laptop'
import {
  COPY_PATH,
  HALL_WITH_TWO_TABLES,
  SCHANK_SHAPE_ID,
  TABLE_A1,
  TABLE_A2,
  TABLE_A1_SHAPE_ID,
  TABLE_A2_SHAPE_ID,
  TENT,
  editorLaptop,
  isHighlighted,
  mountEditor,
  shapesWithTheHandles,
  theDrawing,
} from './tablePlanEditorFixture'
import { aGeometry, anAdminTable } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

const COPY_ONE = anAdminTable({
  tableId: 'table-copy-1',
  name: 'A3',
  geometry: aGeometry({ x: 30, y: 300, width: 80, height: 40 }),
})

const COPY_TWO = anAdminTable({
  tableId: 'table-copy-2',
  name: 'A4',
  geometry: aGeometry({ x: 30, y: 370, width: 80, height: 40 }),
})

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

async function pressCtrl(key: 'c' | 'v'): Promise<KeyboardEvent> {
  const event = new KeyboardEvent('keydown', { key, ctrlKey: true, bubbles: true, cancelable: true })
  window.dispatchEvent(event)
  await flushPromises()
  return event
}

async function clickTheShape(id: string, modifiers: { ctrlKey?: boolean } = {}) {
  const stage = theDrawing()
  await tapAt(stage, centreOf(shapeWithId(stage, id)), modifiers)
}

async function rightClickTheShape(id: string) {
  const stage = theDrawing()
  await rightClickAt(stage, centreOf(shapeWithId(stage, id)))
}

async function waitForTheMenu(entry: 'copy-table' | 'paste-table'): Promise<void> {
  await waitUntil(() => expect(isOnScreen(`[data-test="${entry}"]`)).toBe(true))
  onScreen(`[data-test="${entry}"]`).click()
  await flushPromises()
}

describe('copying with the keyboard', () => {
  it('copies the selected table and pastes it one grid step right and down', async () => {
    const laptop = editorLaptop({ areas: twoTables() })
    await mountEditor()
    await clickTheShape(TABLE_A1_SHAPE_ID)

    await pressCtrl('c')
    await pressCtrl('v')

    await waitUntil(() => expect(laptop.writes()).toHaveLength(1))
    expect(laptop.writtenBodies()).toEqual([
      {
        areaId: 'area-halle',
        tables: [
          {
            sourceTableId: 'table-a1',
            geometry: { x: 30, y: 40, width: 80, height: 40, rotation: 0 },
          },
        ],
      },
    ])
  })

  it('does nothing on a station, because a station is never copied', async () => {
    const laptop = editorLaptop({ areas: twoTables() })
    await mountEditor()
    await clickTheShape(SCHANK_SHAPE_ID)

    await pressCtrl('c')
    await pressCtrl('v')

    expect(laptop.writes()).toEqual([])
  })

  it('lets the browser keep the paste key while the clipboard is empty', async () => {
    const laptop = editorLaptop({ areas: twoTables() })
    await mountEditor()

    const event = await pressCtrl('v')

    expect([event.defaultPrevented, laptop.writes()]).toEqual([false, []])
  })

  it('ignores the keys while a dialog is open', async () => {
    const laptop = editorLaptop({ areas: twoTables() })
    await mountEditor()
    await clickTheShape(TABLE_A1_SHAPE_ID)
    await pressCtrl('c')
    onScreen('[data-test="new-table"]').click()
    await flushPromises()

    await pressCtrl('v')

    expect(laptop.writes()).toEqual([])
  })

  it('leaves a paste key pressed in a text field to the browser', async () => {
    const laptop = editorLaptop({ areas: twoTables() })
    await mountEditor()
    await clickTheShape(TABLE_A1_SHAPE_ID)
    await pressCtrl('c')
    const field = document.body.appendChild(document.createElement('input'))
    field.focus()

    const event = new KeyboardEvent('keydown', { key: 'v', ctrlKey: true, bubbles: true, cancelable: true })
    field.dispatchEvent(event)
    await flushPromises()

    expect([event.defaultPrevented, laptop.writes()]).toEqual([false, []])
  })
})

describe('the context menu on a table', () => {
  it('selects the right-clicked table when it is not selected and copies it', async () => {
    const laptop = editorLaptop({ areas: twoTables() })
    await mountEditor()
    await clickTheShape(TABLE_A2_SHAPE_ID)

    await rightClickTheShape(TABLE_A1_SHAPE_ID)
    await waitForTheMenu('copy-table')
    await pressCtrl('v')

    await waitUntil(() => expect(laptop.writes()).toHaveLength(1))
    expect(laptop.writtenBodies()[0]).toEqual({
      areaId: 'area-halle',
      tables: [
        {
          sourceTableId: 'table-a1',
          geometry: { x: 30, y: 40, width: 80, height: 40, rotation: 0 },
        },
      ],
    })
    expect(isHighlighted('table-table-a1')).toBe(true)
  })

  it('copies nothing when the table menu is dismissed without choosing Kopieren', async () => {
    const laptop = editorLaptop({ areas: twoTables() })
    await mountEditor()
    await clickTheShape(TABLE_A1_SHAPE_ID)
    await rightClickTheShape(TABLE_A1_SHAPE_ID)
    await waitUntil(() => expect(isOnScreen('[data-test="copy-table"]')).toBe(true))

    await rightClickAt(theDrawing(), { x: 456, y: 381 })
    await flushPromises()

    await pressCtrl('v')

    expect(laptop.writes()).toEqual([])
    expect(isOnScreen('[data-test="table-plan-menu"]')).toBe(false)
  })

  it('keeps the whole selection when a selected table is right-clicked and copies all of it', async () => {
    const laptop = editorLaptop({
      areas: twoTables(),
      planReply: inTurn(
        answer({ areas: twoTables() }),
        answer({ areas: [{ ...HALL_WITH_TWO_TABLES, tables: [TABLE_A1, TABLE_A2, COPY_ONE, COPY_TWO] }, TENT] }),
      ),
      writeReply: answer({ tableIds: ['table-copy-1', 'table-copy-2'] }, 201),
    })
    await mountEditor()
    await clickTheShape(TABLE_A1_SHAPE_ID)
    await clickTheShape(TABLE_A2_SHAPE_ID, { ctrlKey: true })

    await rightClickTheShape(TABLE_A1_SHAPE_ID)
    const selectionAfterTheRightClick = [
      isHighlighted('table-table-a1'),
      isHighlighted('table-table-a2'),
    ]
    await waitForTheMenu('copy-table')
    await pressCtrl('v')

    await waitUntil(() => expect(laptop.writes()).toHaveLength(1))
    expect(laptop.writtenBodies()[0]).toEqual({
      areaId: 'area-halle',
      tables: [
        {
          sourceTableId: 'table-a1',
          geometry: { x: 30, y: 40, width: 80, height: 40, rotation: 0 },
        },
        {
          sourceTableId: 'table-a2',
          geometry: { x: 30, y: 110, width: 80, height: 40, rotation: 0 },
        },
      ],
    })
    expect(selectionAfterTheRightClick).toEqual([true, true])
    await waitUntil(() => expect(isHighlighted('table-table-copy-1')).toBe(true))
    expect(isHighlighted('table-table-copy-2')).toBe(true)
  })
})

describe('the context menu on an empty spot', () => {
  it('pastes the copied group with its top left at the clicked drawing point', async () => {
    const laptop = editorLaptop({ areas: twoTables() })
    await mountEditor()
    await clickTheShape(TABLE_A1_SHAPE_ID)
    await pressCtrl('c')

    await rightClickAt(theDrawing(), { x: 456, y: 381 })
    await waitForTheMenu('paste-table')

    await waitUntil(() => expect(laptop.writes()).toHaveLength(1))
    expect(laptop.writtenBodies()[0]).toEqual({
      areaId: 'area-halle',
      tables: [
        {
          sourceTableId: 'table-a1',
          geometry: { x: 300, y: 250, width: 80, height: 40, rotation: 0 },
        },
      ],
    })
  })

  it('shows no paste entry while the clipboard is empty', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()

    await rightClickAt(theDrawing(), { x: 456, y: 381 })
    await flushPromises()

    expect(isOnScreen('[data-test="table-plan-menu"]')).toBe(false)
  })

  it('offers no paste on a station, even with something copied', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()
    await clickTheShape(TABLE_A1_SHAPE_ID)
    await pressCtrl('c')

    await rightClickTheShape(SCHANK_SHAPE_ID)
    await waitUntil(() => expect(isOnScreen('[data-test="table-plan-menu"]')).toBe(true))

    expect(isOnScreen('[data-test="paste-table"]')).toBe(false)
  })

  it('suppresses the browser menu over the drawing, and only there', async () => {
    editorLaptop({ areas: twoTables() })
    await mountEditor()

    const onTheDrawing = new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 100, clientY: 100 })
    theDrawing().content.dispatchEvent(onTheDrawing)
    const besideTheDrawing = new MouseEvent('contextmenu', { bubbles: true, cancelable: true, clientX: 100, clientY: 100 })
    onScreen('[data-test="table-plan-toolbar"]').dispatchEvent(besideTheDrawing)

    expect([onTheDrawing.defaultPrevented, besideTheDrawing.defaultPrevented]).toEqual([true, false])
  })
})

describe('the plan after a paste', () => {
  it('selects the new tables and scrolls the first one into view', async () => {
    editorLaptop({
      areas: twoTables(),
      planReply: inTurn(
        answer({ areas: [HALL_WITH_TWO_TABLES, TENT] }),
        answer({ areas: [{ ...HALL_WITH_TWO_TABLES, tables: [TABLE_A1, TABLE_A2, COPY_ONE, COPY_TWO] }, TENT] }),
      ),
      writeReply: answer({ tableIds: ['table-copy-1', 'table-copy-2'] }, 201),
    })
    await mountEditor('de', [], HALL_WITH_TWO_TABLES.areaId, { width: 612, height: 150 })
    await clickTheShape(TABLE_A1_SHAPE_ID)
    await clickTheShape(TABLE_A2_SHAPE_ID, { ctrlKey: true })
    await pressCtrl('c')

    await pressCtrl('v')

    await waitUntil(() => expect(isHighlighted('table-table-copy-1')).toBe(true))
    expect([
      isHighlighted('table-table-copy-2'),
      shapesWithTheHandles(),
      onScreen('[data-test="drawing-scroll"]').scrollTop > 0,
    ]).toEqual([true, [], true])
  })

  it('shows the reason above the drawing when the laptop refuses the paste', async () => {
    const laptop = editorLaptop({
      areas: twoTables(),
      writeReply: refusal('errors.admin.festivals.tableCopyNameTooLong'),
    })
    await mountEditor()
    await clickTheShape(TABLE_A1_SHAPE_ID)
    await pressCtrl('c')
    laptop.forgetCalls()

    await pressCtrl('v')

    await waitUntil(() =>
      expect(textOnScreen('[data-test="plan-refusal"]').trim()).toBe(
        'Der Name einer Kopie wäre länger als 40 Zeichen. Kürzen Sie den Namen des Tisches.',
      ),
    )
    expect(laptop.callsTo('POST', COPY_PATH)).toHaveLength(1)
    expect(laptop.callsTo('GET', '/api/admin/festivals/fest-1/table-plan')).toHaveLength(0)
  })
})
