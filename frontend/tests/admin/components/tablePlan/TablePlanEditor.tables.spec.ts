import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount } from '@vue/test-utils'
import {
  clickOn,
  dialogText,
  inputOf,
  leave,
  onScreen,
  pressInDialog,
  typeIn,
  waitForDialog,
  waitUntil,
} from '../../../support/dom'
import { aHold, answer, heldUntil, inTurn, refusal } from '../../../support/laptop'
import {
  HALL,
  TABLE_A1,
  TABLE_A1_SHAPE_ID,
  TENT,
  editorLaptop,
  mountEditor,
  selectTheShape,
} from './tablePlanEditorFixture'
import { aGeometry, anAdminTable } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

const TABLES_PATH = '/api/admin/festivals/fest-1/tables'

const NAME_TAKEN =
  'Ein Tisch dieses Festes hat diesen Namen schon, egal ob er sichtbar oder ausgeblendet ist. Wählen Sie einen anderen Namen.'

const TABLE_A2 = anAdminTable({ tableId: 'table-a2', name: 'A2', geometry: aGeometry({ x: 20, y: 100 }) })

const TABLE_A2_SHAPE_ID = 'table-table-a2'

async function renameTableA1To(name: string): Promise<void> {
  await selectTheShape(TABLE_A1_SHAPE_ID)
  await typeIn('[data-test="table-name-field"]', name)
  leave('[data-test="table-name-field"]')
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('adding a table', () => {
  it('offers an empty name field, because the admin names every table', async () => {
    editorLaptop()

    await mountEditor()

    expect(inputOf('[data-test="new-table-name"]').value).toBe('')
    expect(onScreen('[data-test="add-table"]').hasAttribute('disabled')).toBe(true)
  })

  it('sends the typed name and puts the table below the lowest shape of the area', async () => {
    const laptop = editorLaptop({ writeReply: answer({ tableId: 'table-a2' }, 201) })
    await mountEditor()

    await typeIn('[data-test="new-table-name"]', 'A2')
    await clickOn('[data-test="add-table"]')

    expect(laptop.writes()).toEqual([
      {
        url: TABLES_PATH,
        method: 'POST',
        body: {
          areaId: 'area-halle',
          name: 'A2',
          geometry: { x: 10, y: 80, width: 80, height: 40, rotation: 0 },
        },
      },
    ])
  })

  it('selects the new table once the laptop has it', async () => {
    const tableA2 = anAdminTable({ tableId: 'table-a2', name: 'A2', geometry: aGeometry({ x: 10, y: 80 }) })
    editorLaptop({
      writeReply: answer({ tableId: 'table-a2' }, 201),
      planReply: inTurn(
        answer({ areas: [HALL, TENT] }),
        answer({ areas: [{ ...HALL, tables: [TABLE_A1, tableA2] }, TENT] }),
      ),
    })
    await mountEditor()

    await typeIn('[data-test="new-table-name"]', 'A2')
    await clickOn('[data-test="add-table"]')

    await waitUntil(() => expect(inputOf('[data-test="table-name-field"]').value).toBe('A2'))
  })

  it('shows the reason under the field when another table has the name', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.tableNameTaken') })
    await mountEditor()

    await typeIn('[data-test="new-table-name"]', 'a1')
    await clickOn('[data-test="add-table"]')

    await waitUntil(() =>
      expect(onScreen('[data-test="table-plan-adding"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Ein Tisch dieses Festes hat diesen Namen schon, egal ob er sichtbar oder ausgeblendet ist. Wählen Sie einen anderen Namen.',
      ),
    )
  })
})

describe('the selected table', () => {
  it('is renamed when the admin leaves its name field', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)

    await typeIn('[data-test="table-name-field"]', 'A12')
    leave('[data-test="table-name-field"]')

    await waitUntil(() =>
      expect(laptop.writes()).toEqual([
        {
          url: `${TABLES_PATH}/table-a1`,
          method: 'PUT',
          body: {
            areaId: 'area-halle',
            name: 'A12',
            geometry: { x: 20, y: 30, width: 80, height: 40, rotation: 0 },
          },
        },
      ]),
    )
  })

  it('never sends an empty name and says the table needs one', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)

    await typeIn('[data-test="table-name-field"]', ' ')
    leave('[data-test="table-name-field"]')

    await waitUntil(() =>
      expect(onScreen('[data-test="shape-details"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Geben Sie dem Tisch einen Namen.',
      ),
    )
    expect(laptop.writes()).toEqual([])
  })

  it('is hidden', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)

    await clickOn('[data-test="hide-table"]')

    expect(laptop.writes()).toEqual([{ url: `${TABLES_PATH}/table-a1/hide`, method: 'POST', body: undefined }])
  })

  it('is shown again when it was hidden', async () => {
    const laptop = editorLaptop({ areas: [{ ...HALL, tables: [{ ...TABLE_A1, isHidden: true }] }] })
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)

    await clickOn('[data-test="show-table"]')

    expect(laptop.writes()).toEqual([{ url: `${TABLES_PATH}/table-a1/show`, method: 'POST', body: undefined }])
  })

  it('shows the reason beside the drawing when the laptop refuses to hide it', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.tableWithItemsToHandOutOrSettleStaysShown') })
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)

    await clickOn('[data-test="hide-table"]')

    await waitUntil(() =>
      expect(onScreen('[data-test="shape-details"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Solange das Fest aktiv ist, lässt sich ein Tisch erst ausblenden, wenn dort alles zubereitet und abgerechnet ist.',
      ),
    )
  })
})

describe('deleting the selected table', () => {
  it('asks first, naming the table', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)

    await clickOn('[data-test="delete-table"]')
    await waitForDialog()

    expect(dialogText('[data-test="confirm-title"]')).toBe('Tisch A1 löschen')
    expect(laptop.writes()).toEqual([])
  })

  it('deletes the table once the admin confirms', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)
    await clickOn('[data-test="delete-table"]')

    await pressInDialog('[data-test="confirm"]')

    expect(laptop.writes()).toEqual([{ url: `${TABLES_PATH}/table-a1`, method: 'DELETE', body: undefined }])
  })

  it('shows the reason beside the drawing when the table has orders', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.tableWithOrdersStays') })
    await mountEditor()
    await selectTheShape(TABLE_A1_SHAPE_ID)
    await clickOn('[data-test="delete-table"]')

    await pressInDialog('[data-test="confirm"]')

    await waitUntil(() =>
      expect(onScreen('[data-test="shape-details"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Ein Tisch, an dem schon bestellt wurde, kann nicht gelöscht werden. Blenden Sie ihn stattdessen aus.',
      ),
    )
  })
})

describe('a refused rename the admin has walked away from', () => {
  it('keeps the reason on screen when the admin taps another table before the laptop answers', async () => {
    const answerHeldBack = aHold()
    editorLaptop({
      areas: [{ ...HALL, tables: [TABLE_A1, TABLE_A2] }, TENT],
      writeReply: heldUntil(answerHeldBack.released, refusal('errors.admin.festivals.tableNameTaken')),
    })
    await mountEditor()
    await renameTableA1To('A2')
    await selectTheShape(TABLE_A2_SHAPE_ID)

    answerHeldBack.release()

    await waitUntil(() => expect(document.body.textContent).toContain(NAME_TAKEN))
  })

  it('keeps the reason on screen when the admin taps another table after the laptop answered', async () => {
    editorLaptop({
      areas: [{ ...HALL, tables: [TABLE_A1, TABLE_A2] }, TENT],
      writeReply: refusal('errors.admin.festivals.tableNameTaken'),
    })
    await mountEditor()
    await renameTableA1To('A2')
    await waitUntil(() => expect(document.body.textContent).toContain(NAME_TAKEN))

    await selectTheShape(TABLE_A2_SHAPE_ID)

    expect(document.body.textContent).toContain(NAME_TAKEN)
  })

  it('keeps the name the admin typed on that table, so it can be corrected', async () => {
    editorLaptop({
      areas: [{ ...HALL, tables: [TABLE_A1, TABLE_A2] }, TENT],
      writeReply: refusal('errors.admin.festivals.tableNameTaken'),
    })
    await mountEditor()
    await renameTableA1To('A2')
    await waitUntil(() => expect(document.body.textContent).toContain(NAME_TAKEN))
    await selectTheShape(TABLE_A2_SHAPE_ID)

    await selectTheShape(TABLE_A1_SHAPE_ID)

    expect(inputOf('[data-test="table-name-field"]').value).toBe('A2')
  })
})
