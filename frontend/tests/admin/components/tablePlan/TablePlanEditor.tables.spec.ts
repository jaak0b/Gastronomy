import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount } from '@vue/test-utils'
import {
  allOnScreen,
  clickOn,
  dialogText,
  iconActionLabelsOnScreen,
  inputOf,
  isOnScreen,
  onScreen,
  pressInDialog,
  textOnScreen,
  typeIn,
  waitForDialog,
  waitUntil,
} from '../../../support/dom'
import { saveTheTablePlanNameDialog } from '../../../support/formDialogs'
import { dragBy, hasShapeWithId, shapeWithId } from '../../../support/konvaDrawing'
import { answer, inTurn, refusal } from '../../../support/laptop'
import {
  HALL,
  TABLE_A1,
  TABLE_A1_SHAPE_ID,
  TENT,
  chooseInTheMenuOf,
  editorLaptop,
  mountEditor,
  openTheToolbarMenu,
  planRefusalText,
  pressInTheToolbar,
  selectTheShape,
  shapesWithTheHandles,
  theDrawing,
} from './tablePlanEditorFixture'
import { aGeometry, anAdminTable } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

const TABLES_PATH = '/api/admin/festivals/fest-1/tables'

const NAME_TAKEN =
  'Der Name ist in diesem Fest bereits an einen Tisch vergeben, auch an einen ausgeblendeten. Wählen Sie einen anderen Namen.'

const STILL_OPEN =
  'Solange ein Fest aktiv ist, kann ein Tisch erst ausgeblendet werden, wenn alle seine Positionen zubereitet und kassiert sind.'

const FAILED =
  'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal oder laden Sie die Seite neu.'

const HIDDEN_A3 = anAdminTable({ tableId: 'table-a3', name: 'A3', isHidden: true, geometry: aGeometry({ y: 300 }) })

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('the toolbar above the drawing', () => {
  it('offers a new table, a station and the hidden tables from left to right', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, HIDDEN_A3] }, TENT] })

    await mountEditor()

    expect(
      allOnScreen('[data-test="table-plan-toolbar"] .v-btn').map((button) => button.getAttribute('data-test')),
    ).toEqual(['new-table', 'add-station', 'hidden-tables'])
  })

  it('sits inside the frame of the drawing, as its top strip', async () => {
    editorLaptop()

    await mountEditor()

    expect(isOnScreen('[data-test="plan-frame"] [data-test="table-plan-toolbar"] + [data-test="table-plan-drawing-column"]')).toBe(true)
  })
})

describe('a new table', () => {
  it('opens an empty dialog', async () => {
    editorLaptop()
    await mountEditor()

    await pressInTheToolbar('new-table')

    expect([dialogText('[data-test="form-dialog-title"]'), inputOf('[data-test="table-plan-name-field"]').value]).toEqual([
      'Neuer Tisch',
      '',
    ])
  })

  it('cannot be saved while the name is blank', async () => {
    editorLaptop()
    await mountEditor()
    await pressInTheToolbar('new-table')

    await typeIn('[data-test="table-plan-name-field"]', '  ')

    expect(onScreen('[data-test="form-save"]').hasAttribute('disabled')).toBe(true)
  })

  it('sends the typed name and puts the table below the lowest shape in the drawing', async () => {
    const laptop = editorLaptop({ writeReply: answer({ tableId: 'table-a2' }, 201) })
    await mountEditor()
    await pressInTheToolbar('new-table')

    await saveTheTablePlanNameDialog('A2')

    expect(laptop.writes()).toEqual([
      {
        url: TABLES_PATH,
        method: 'POST',
        body: { areaId: 'area-halle', name: 'A2', geometry: { x: 10, y: 80, width: 80, height: 40, rotation: 0 } },
      },
    ])
  })

  it('goes below the lowest table of the area, a hidden one included, so it never takes the place of a hidden table', async () => {
    const hiddenA2 = anAdminTable({ tableId: 'table-a2', name: 'A2', isHidden: true, geometry: aGeometry({ x: 10, y: 80 }) })
    const laptop = editorLaptop({
      areas: [{ ...HALL, tables: [TABLE_A1, hiddenA2] }, TENT],
      writeReply: answer({ tableId: 'table-a3' }, 201),
    })
    await mountEditor()
    await pressInTheToolbar('new-table')

    await saveTheTablePlanNameDialog('A3')

    expect(laptop.writtenBodies()).toEqual([
      { areaId: 'area-halle', name: 'A3', geometry: { x: 10, y: 130, width: 80, height: 40, rotation: 0 } },
    ])
  })

  it('is selected in the drawing once the laptop has it', async () => {
    const tableA2 = anAdminTable({ tableId: 'table-a2', name: 'A2', geometry: aGeometry({ x: 10, y: 80 }) })
    editorLaptop({
      writeReply: answer({ tableId: 'table-a2' }, 201),
      planReply: inTurn(
        answer({ areas: [HALL, TENT] }),
        answer({ areas: [{ ...HALL, tables: [TABLE_A1, tableA2] }, TENT] }),
      ),
    })
    await mountEditor()
    await pressInTheToolbar('new-table')

    await saveTheTablePlanNameDialog('A2')

    await waitUntil(() => expect(shapesWithTheHandles()).toEqual(['table-table-a2']))
  })

  it('keeps the dialog open with the reason when another table has the name', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.tableNameTaken') })
    await mountEditor()
    await pressInTheToolbar('new-table')

    await saveTheTablePlanNameDialog('a1')

    await waitUntil(() =>
      expect(textOnScreen('[data-test="form-dialog"] [data-test="refusal"]').trim()).toBe(NAME_TAKEN),
    )
  })
})

describe('editing a table from its menu', () => {
  it('opens the dialog with the name the laptop keeps', async () => {
    editorLaptop()
    await mountEditor()

    await chooseInTheMenuOf(TABLE_A1_SHAPE_ID, 'edit-table-from-menu')

    expect([dialogText('[data-test="form-dialog-title"]'), inputOf('[data-test="table-plan-name-field"]').value]).toEqual([
      'Tisch bearbeiten',
      'A1',
    ])
  })

  it('sends the new name with where the table is drawn', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await chooseInTheMenuOf(TABLE_A1_SHAPE_ID, 'edit-table-from-menu')

    await saveTheTablePlanNameDialog('A12')

    expect(laptop.writes()).toEqual([
      {
        url: `${TABLES_PATH}/table-a1`,
        method: 'PUT',
        body: { areaId: 'area-halle', name: 'A12', geometry: { x: 20, y: 30, width: 80, height: 40, rotation: 0 } },
      },
    ])
  })
})

describe('hiding a table from its menu', () => {
  it('asks first, as the festival list does, and says what follows', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await chooseInTheMenuOf(TABLE_A1_SHAPE_ID, 'remove-table-from-menu')
    await waitForDialog()

    expect([dialogText('[data-test="confirm-title"]'), dialogText('[data-test="confirm-body"]')]).toEqual([
      'Tisch ausblenden',
      'Die Kellner sehen diesen Tisch dann nicht mehr.',
    ])
    expect(laptop.writes()).toEqual([])
  })

  it('hides the table once the admin confirms', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await chooseInTheMenuOf(TABLE_A1_SHAPE_ID, 'remove-table-from-menu')

    await pressInDialog('[data-test="confirm"]')

    expect(laptop.writes()).toEqual([{ url: `${TABLES_PATH}/table-a1/hide`, method: 'POST', body: undefined }])
  })

  it('takes a hidden table out of the drawing and lets go of it', async () => {
    editorLaptop({
      planReply: inTurn(
        answer({ areas: [HALL, TENT] }),
        answer({ areas: [{ ...HALL, tables: [{ ...TABLE_A1, isHidden: true }] }, TENT] }),
      ),
    })
    await mountEditor()
    await chooseInTheMenuOf(TABLE_A1_SHAPE_ID, 'remove-table-from-menu')

    await pressInDialog('[data-test="confirm"]')

    await waitUntil(() => expect(hasShapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)).toBe(false))
    expect(shapesWithTheHandles()).toEqual([])
  })

  it('shows the reason above the drawing when the laptop still refuses to hide it', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.tableWithItemsToHandOutOrSettleStaysShown') })
    await mountEditor()
    await chooseInTheMenuOf(TABLE_A1_SHAPE_ID, 'remove-table-from-menu')

    await pressInDialog('[data-test="confirm"]')

    await waitUntil(() => expect(planRefusalText()).toBe(STILL_OPEN))
  })
})

describe('the one refusal line above the drawing', () => {
  it('shows only the newest refusal when a refused hide follows a refused move', async () => {
    editorLaptop({
      writeReply: inTurn(
        refusal('errors.admin.actionFailed', { status: 400 }),
        refusal('errors.admin.festivals.tableWithItemsToHandOutOrSettleStaysShown'),
      ),
    })
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)
    await dragBy(theDrawing(), shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID), { x: 75, y: 150 })
    await waitUntil(() => expect(planRefusalText()).toBe(FAILED))

    await chooseInTheMenuOf(TABLE_A1_SHAPE_ID, 'remove-table-from-menu')
    await pressInDialog('[data-test="confirm"]')

    await waitUntil(() => expect(planRefusalText()).toBe(STILL_OPEN))
    expect(allOnScreen('.v-alert')).toHaveLength(1)
  })

  it('goes away when the admin selects another shape', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.actionFailed', { status: 400 }) })
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)
    await dragBy(theDrawing(), shapeWithId(theDrawing(), TABLE_A1_SHAPE_ID), { x: 75, y: 150 })
    await waitUntil(() => expect(isOnScreen('[data-test="plan-refusal"]')).toBe(true))

    await selectTheShape('station-station-schank')

    await waitUntil(() => expect(isOnScreen('[data-test="plan-refusal"]')).toBe(false))
  })
})

describe('the hidden tables of the area', () => {
  it('are offered in the dropdown at the right of the toolbar, and only they', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, HIDDEN_A3] }, TENT] })
    await mountEditor()

    await openTheToolbarMenu('hidden-tables', 'hidden-table')

    expect(allOnScreen('[data-test="hidden-table"]').map((row) => row.textContent?.trim())).toEqual(['A3'])
  })

  it('are named Ausgeblendete Tische in the toolbar', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, HIDDEN_A3] }, TENT] })

    await mountEditor()

    expect(textOnScreen('[data-test="hidden-tables"]').trim()).toBe('Ausgeblendete Tische')
  })

  it('offer each one the open eye that shows it again', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, HIDDEN_A3] }, TENT] })
    await mountEditor()
    await openTheToolbarMenu('hidden-tables', 'hidden-table')

    onScreen('[data-test="show-table"]').dispatchEvent(new MouseEvent('mouseenter'))

    expect(isOnScreen('[data-test="hidden-table"][data-test-id="table-a3"] [data-test="show-table"] .mdi-eye')).toBe(true)
    await waitUntil(() => expect(iconActionLabelsOnScreen()).toContain('Einblenden'))
  })

  it('leave no dropdown in the toolbar while the area has no hidden table', async () => {
    editorLaptop()

    await mountEditor()

    expect(isOnScreen('[data-test="hidden-tables"]')).toBe(false)
  })

  it('show a hidden table again without asking', async () => {
    const laptop = editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, HIDDEN_A3] }, TENT] })
    await mountEditor()
    await openTheToolbarMenu('hidden-tables', 'hidden-table')

    await clickOn('[data-test="hidden-table"][data-test-id="table-a3"] [data-test="show-table"]')

    expect(laptop.writes()).toEqual([{ url: `${TABLES_PATH}/table-a3/show`, method: 'POST', body: undefined }])
  })

  it('draw a table on the plan again once the laptop shows it', async () => {
    editorLaptop({
      planReply: inTurn(
        answer({ areas: [{ ...HALL, tables: [TABLE_A1, HIDDEN_A3] }, TENT] }),
        answer({ areas: [{ ...HALL, tables: [TABLE_A1, { ...HIDDEN_A3, isHidden: false }] }, TENT] }),
      ),
    })
    await mountEditor()
    await openTheToolbarMenu('hidden-tables', 'hidden-table')

    await clickOn('[data-test="hidden-table"][data-test-id="table-a3"] [data-test="show-table"]')

    await waitUntil(() => expect(hasShapeWithId(theDrawing(), 'table-table-a3')).toBe(true))
  })
})
