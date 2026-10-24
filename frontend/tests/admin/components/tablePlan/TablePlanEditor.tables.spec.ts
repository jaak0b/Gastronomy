import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import {
  allOnScreen,
  clickOn,
  iconActionLabelsOnScreen,
  dialogText,
  inputOf,
  isOnScreen,
  onScreen,
  pressInDialog,
  refusalReasonsOnScreen,
  textOnScreen,
  typeIn,
  waitForDialog,
  waitUntil,
} from '../../../support/dom'
import { saveTheTableDialog } from '../../../support/formDialogs'
import { hasShapeWithId } from '../../../support/konvaDrawing'
import { answer, inTurn, refusal } from '../../../support/laptop'
import {
  HALL,
  TABLE_A1,
  TABLE_A1_SHAPE_ID,
  TENT,
  editorLaptop,
  mountEditor,
  selectTheShape,
  shapesWithTheHandles,
  tableRow,
  theDrawing,
} from './tablePlanEditorFixture'
import { aFestival, aGeometry, anAdminTable } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

const TABLES_PATH = '/api/admin/festivals/fest-1/tables'

const NAME_TAKEN =
  'Ein Tisch dieses Festes hat diesen Namen schon, egal ob er sichtbar oder ausgeblendet ist. Wählen Sie einen anderen Namen.'

const STILL_OPEN =
  'Ein Tisch kann bei einem aktiven Fest erst ausgeblendet werden, wenn dort alles zubereitet und abgerechnet ist.'

const TABLE_A2 = anAdminTable({ tableId: 'table-a2', name: 'A2', geometry: aGeometry({ x: 20, y: 100 }) })

const HIDDEN_A3 = anAdminTable({ tableId: 'table-a3', name: 'A3', isHidden: true, geometry: aGeometry({ y: 300 }) })

const A1_WITH_ITEMS_TO_HAND_OUT = { ...TABLE_A1, hasItemsToHandOutOrSettle: true }

const TABLE_LIST = '[data-test="table-plan-table-list"]'

function hideButtonOf(tableId: string): HTMLButtonElement {
  return onScreen('[data-test="hide-entry"]', tableRow(tableId)) as HTMLButtonElement
}

async function openTheNewTableDialog(): Promise<void> {
  await clickOn('[data-test="new-entry"]', onScreen(TABLE_LIST))
  await flushPromises()
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('the tables of the area', () => {
  it('are listed by name beside the drawing in the order of the laptop', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, TABLE_A2] }, TENT] })

    await mountEditor()

    expect(allOnScreen('[data-test="table-row"] [data-test="entry-name"]').map((name) => name.textContent?.trim())).toEqual([
      'A1',
      'A2',
    ])
  })

  it('leave a hidden table out of the list until the admin asks for hidden tables', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, HIDDEN_A3] }, TENT] })

    await mountEditor()

    expect(isOnScreen('[data-test="table-row"][data-test-id="table-a3"]')).toBe(false)
  })

  it('show a hidden table, marked hidden and offered to show again, once the box for hidden tables is ticked', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, HIDDEN_A3] }, TENT] })
    await mountEditor()

    await clickOn('[data-test="show-hidden-entries"] input', onScreen(TABLE_LIST))
    onScreen('[data-test="show-entry"]', tableRow('table-a3')).dispatchEvent(new MouseEvent('mouseenter'))

    expect([
      textOnScreen('[data-test="hidden"]', tableRow('table-a3')).trim(),
      isOnScreen('[data-test="show-entry"] .mdi-eye', tableRow('table-a3')),
    ]).toEqual(['Ausgeblendet', true])
    await waitUntil(() =>
      expect(iconActionLabelsOnScreen()).toContain('Einblenden'),
    )
  })
})

describe('a table the admin taps in the drawing', () => {
  it('highlights its row', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, TABLE_A2] }, TENT] })
    await mountEditor()

    await selectTheShape('table-table-a2')

    expect(tableRow('table-a2').classList.contains('selected-row')).toBe(true)
  })

  it('brings its row into view in the list beside the drawing', async () => {
    const scrolledIntoView = vi.spyOn(Element.prototype, 'scrollIntoView')
    editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, TABLE_A2] }, TENT] })
    await mountEditor()

    await selectTheShape('table-table-a2')

    expect(scrolledIntoView.mock.contexts).toContain(tableRow('table-a2'))
  })
})

describe('a new table', () => {
  it('opens an empty dialog', async () => {
    editorLaptop()
    await mountEditor()

    await openTheNewTableDialog()

    expect([dialogText('[data-test="form-dialog-title"]'), inputOf('[data-test="table-name-field"]').value]).toEqual([
      'Neuer Tisch',
      '',
    ])
  })

  it('cannot be saved while the name is blank', async () => {
    editorLaptop()
    await mountEditor()
    await openTheNewTableDialog()

    await typeIn('[data-test="table-name-field"]', '  ')

    expect(onScreen('[data-test="form-save"]').hasAttribute('disabled')).toBe(true)
  })

  it('sends the typed name and puts the table below the lowest shape in the drawing', async () => {
    const laptop = editorLaptop({ writeReply: answer({ tableId: 'table-a2' }, 201) })
    await mountEditor()
    await openTheNewTableDialog()

    await saveTheTableDialog('A2')

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
    await openTheNewTableDialog()

    await saveTheTableDialog('A3')

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
    await openTheNewTableDialog()

    await saveTheTableDialog('A2')

    await waitUntil(() => expect(shapesWithTheHandles()).toEqual(['table-table-a2']))
  })

  it('keeps the dialog open with the reason when another table has the name', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.tableNameTaken') })
    await mountEditor()
    await openTheNewTableDialog()

    await saveTheTableDialog('a1')

    await waitUntil(() =>
      expect(textOnScreen('[data-test="form-dialog"] [data-test="refusal"]').trim()).toBe(NAME_TAKEN),
    )
  })
})

describe('editing a table', () => {
  it('opens the dialog with the name the laptop keeps', async () => {
    editorLaptop()
    await mountEditor()

    await clickOn('[data-test="edit-entry"]', tableRow('table-a1'))
    await flushPromises()

    expect([dialogText('[data-test="form-dialog-title"]'), inputOf('[data-test="table-name-field"]').value]).toEqual([
      'Tisch bearbeiten',
      'A1',
    ])
  })

  it('sends the new name with where the table is drawn', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await clickOn('[data-test="edit-entry"]', tableRow('table-a1'))
    await flushPromises()

    await saveTheTableDialog('A12')

    expect(laptop.writes()).toEqual([
      {
        url: `${TABLES_PATH}/table-a1`,
        method: 'PUT',
        body: { areaId: 'area-halle', name: 'A12', geometry: { x: 20, y: 30, width: 80, height: 40, rotation: 0 } },
      },
    ])
  })
})

describe('hiding and showing a table', () => {
  it('asks first, as the festival list does, and says what follows', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await clickOn('[data-test="hide-entry"]', tableRow('table-a1'))
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
    await clickOn('[data-test="hide-entry"]', tableRow('table-a1'))

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
    await selectTheShape(TABLE_A1_SHAPE_ID)
    await clickOn('[data-test="hide-entry"]', tableRow('table-a1'))

    await pressInDialog('[data-test="confirm"]')

    await waitUntil(() => expect(hasShapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)).toBe(false))
    expect(shapesWithTheHandles()).toEqual([])
  })

  it('cannot be asked for while the festival runs and the table still has items to hand out or settle', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [A1_WITH_ITEMS_TO_HAND_OUT] }, TENT] })

    await mountEditor()

    expect(hideButtonOf('table-a1').disabled).toBe(true)
  })

  it('names the reason on the disabled trash icon', async () => {
    editorLaptop({ areas: [{ ...HALL, tables: [A1_WITH_ITEMS_TO_HAND_OUT] }, TENT] })
    await mountEditor()

    onScreen('[data-test="hide-entry-wrapper"]', tableRow('table-a1')).dispatchEvent(new MouseEvent('mouseenter'))

    await waitUntil(() =>
      expect(refusalReasonsOnScreen()).toContain(
        STILL_OPEN,
      ),
    )
  })

  it('can be asked for once the festival is over, whatever the table still has', async () => {
    editorLaptop({
      festival: aFestival({ festivalId: 'fest-1', isRunning: false }),
      areas: [{ ...HALL, tables: [A1_WITH_ITEMS_TO_HAND_OUT] }, TENT],
    })

    await mountEditor()

    expect(hideButtonOf('table-a1').disabled).toBe(false)
  })

  it('shows the reason on the row when the laptop still refuses to hide it', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.tableWithItemsToHandOutOrSettleStaysShown') })
    await mountEditor()
    await clickOn('[data-test="hide-entry"]', tableRow('table-a1'))

    await pressInDialog('[data-test="confirm"]')

    await waitUntil(() =>
      expect(tableRow('table-a1').querySelector('[data-test="refusal"]')?.textContent?.trim()).toBe(STILL_OPEN),
    )
  })

  it('shows a hidden table again where it stood, without asking and without moving anything', async () => {
    const laptop = editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, HIDDEN_A3] }, TENT] })
    await mountEditor()
    await clickOn('[data-test="show-hidden-entries"] input', onScreen(TABLE_LIST))

    await clickOn('[data-test="show-entry"]', tableRow('table-a3'))

    expect(laptop.writes()).toEqual([{ url: `${TABLES_PATH}/table-a3/show`, method: 'POST', body: undefined }])
  })
})
