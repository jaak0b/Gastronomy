import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { useConnectionStore } from '../../../../src/shared/stores/connection'
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
import { hasShapeWithId } from '../../../support/konvaDrawing'
import { answer, inTurn, refusal } from '../../../support/laptop'
import {
  HALL,
  TABLE_A1_SHAPE_ID,
  TENT,
  areaRow,
  editorLaptop,
  mountEditor,
  theDrawing,
} from './tablePlanEditorFixture'
import { anAdminArea } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

const AREAS_PATH = '/api/admin/festivals/fest-1/areas'

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the areas of the table plan', () => {
  it('opens on the first area with its drawing', async () => {
    editorLaptop()

    await mountEditor()

    expect(areaRow('area-halle').classList.contains('selected-row')).toBe(true)
    expect(hasShapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)).toBe(true)
  })

  it('draws the area the admin picks', async () => {
    editorLaptop()
    await mountEditor()

    await clickOn('[data-test="area-row"][data-test-id="area-zelt"] [data-test="select-area"]')

    await waitUntil(() => expect(hasShapeWithId(theDrawing(), TABLE_A1_SHAPE_ID)).toBe(false))
  })

  it('lists a hidden area greyed and marked hidden', async () => {
    editorLaptop({ areas: [HALL, { ...TENT, isHidden: true }] })

    await mountEditor()

    expect(areaRow('area-zelt').querySelector('[data-test="hidden"]')?.textContent?.trim()).toBe('Ausgeblendet')
    expect(areaRow('area-zelt').querySelector('[data-test="select-area"]')?.classList.contains('text-medium-emphasis')).toBe(true)
  })
})

describe('creating an area', () => {
  it('sends the typed name as the last area in the order', async () => {
    const laptop = editorLaptop({ writeReply: answer({ areaId: 'area-garten' }, 201) })
    await mountEditor()

    await typeIn('[data-test="new-area-name"]', 'Garten')
    await clickOn('[data-test="create-area"]')

    expect(laptop.writes()).toEqual([{ url: AREAS_PATH, method: 'POST', body: { name: 'Garten', sortOrder: 3 } }])
  })

  it('opens the new area once the laptop has it', async () => {
    const garden = anAdminArea({ areaId: 'area-garten', name: 'Garten', sortOrder: 3 })
    editorLaptop({
      writeReply: answer({ areaId: 'area-garten' }, 201),
      planReply: inTurn(answer({ areas: [HALL, TENT] }), answer({ areas: [HALL, TENT, garden] })),
    })
    await mountEditor()

    await typeIn('[data-test="new-area-name"]', 'Garten')
    await clickOn('[data-test="create-area"]')

    await waitUntil(() => expect(areaRow('area-garten').classList.contains('selected-row')).toBe(true))
  })

  it('cannot be sent while the name is empty', async () => {
    editorLaptop()
    await mountEditor()

    await typeIn('[data-test="new-area-name"]', '   ')

    expect(onScreen('[data-test="create-area"]').hasAttribute('disabled')).toBe(true)
  })

  it('shows the reason under the field when the name is taken', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.areaNameTaken') })
    await mountEditor()

    await typeIn('[data-test="new-area-name"]', 'halle')
    await clickOn('[data-test="create-area"]')

    await waitUntil(() =>
      expect(onScreen('.create-area [data-test="refusal"]').textContent?.trim()).toBe(
        'Ein Bereich dieses Festes hat diesen Namen schon, egal ob er sichtbar oder ausgeblendet ist. Wählen Sie einen anderen Namen.',
      ),
    )
  })
})

describe('renaming the selected area', () => {
  it('sends the new name with its place in the order when the field is left', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await typeIn('[data-test="area-name-field"]', 'Große Halle')
    leave('[data-test="area-name-field"]')

    await waitUntil(() =>
      expect(laptop.writes()).toEqual([
        { url: `${AREAS_PATH}/area-halle`, method: 'PUT', body: { name: 'Große Halle', sortOrder: 1 } },
      ]),
    )
  })

  it('keeps what the admin is typing when the plan is read again', async () => {
    editorLaptop()
    await mountEditor()
    await typeIn('[data-test="area-name-field"]', 'Große Halle')

    await useConnectionStore().refetchAll()
    await flushPromises()

    expect(inputOf('[data-test="area-name-field"]').value).toBe('Große Halle')
  })

  it('never sends an empty name and says the area needs one', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await typeIn('[data-test="area-name-field"]', '')
    leave('[data-test="area-name-field"]')

    await waitUntil(() =>
      expect(areaRow('area-halle').querySelector('[data-test="refusal"]')?.textContent?.trim()).toBe(
        'Geben Sie dem Bereich einen Namen.',
      ),
    )
    expect(laptop.writes()).toEqual([])
  })

  it('keeps the reason under the area when the admin picks another area after a refused name', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.areaNameTaken') })
    await mountEditor()
    await typeIn('[data-test="area-name-field"]', 'Zelt')
    leave('[data-test="area-name-field"]')
    await waitUntil(() => expect(areaRow('area-halle').querySelector('[data-test="refusal"]')).not.toBeNull())

    await clickOn('[data-test="area-row"][data-test-id="area-zelt"] [data-test="select-area"]')

    expect(areaRow('area-halle').querySelector('[data-test="refusal"]')?.textContent?.trim()).toBe(
      'Ein Bereich dieses Festes hat diesen Namen schon, egal ob er sichtbar oder ausgeblendet ist. Wählen Sie einen anderen Namen.',
    )
  })

  it('keeps the refused name the admin typed, so it can be corrected', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.areaNameTaken') })
    await mountEditor()
    await typeIn('[data-test="area-name-field"]', 'Zelt')
    leave('[data-test="area-name-field"]')
    await waitUntil(() => expect(areaRow('area-halle').querySelector('[data-test="refusal"]')).not.toBeNull())
    await clickOn('[data-test="area-row"][data-test-id="area-zelt"] [data-test="select-area"]')

    await clickOn('[data-test="area-row"][data-test-id="area-halle"] [data-test="select-area"]')

    expect(inputOf('[data-test="area-name-field"]').value).toBe('Zelt')
  })
})

describe('the order and visibility of an area', () => {
  it('moves the selected area up by swapping it with the one above', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await clickOn('[data-test="area-row"][data-test-id="area-zelt"] [data-test="select-area"]')

    await clickOn('[data-test="move-area-up"]')

    await waitUntil(() =>
      expect(laptop.writes().map((write) => [write.url, write.body])).toEqual([
        [`${AREAS_PATH}/area-zelt`, { name: 'Zelt', sortOrder: 1 }],
        [`${AREAS_PATH}/area-halle`, { name: 'Halle', sortOrder: 2 }],
      ]),
    )
  })

  it('hides the selected area', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await clickOn('[data-test="hide-area"]')

    expect(laptop.writes()).toEqual([{ url: `${AREAS_PATH}/area-halle/hide`, method: 'POST', body: undefined }])
  })

  it('shows a hidden area again', async () => {
    const laptop = editorLaptop({ areas: [{ ...HALL, isHidden: true }, TENT] })
    await mountEditor()

    await clickOn('[data-test="show-area"]')

    expect(laptop.writes()).toEqual([{ url: `${AREAS_PATH}/area-halle/show`, method: 'POST', body: undefined }])
  })

  it('shows the reason under the area when the laptop refuses to hide it', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.areaWithItemsToHandOutOrSettleStaysShown') })
    await mountEditor()

    await clickOn('[data-test="hide-area"]')

    await waitUntil(() =>
      expect(areaRow('area-halle').querySelector('[data-test="refusal"]')?.textContent?.trim()).toBe(
        'Solange das Fest aktiv ist, lässt sich ein Bereich erst ausblenden, wenn an allen seinen Tischen alles zubereitet und abgerechnet ist.',
      ),
    )
  })
})

describe('deleting an area', () => {
  it('asks first and names what goes with the area', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await clickOn('[data-test="delete-area"]')
    await waitForDialog()

    expect([dialogText('[data-test="confirm-title"]'), dialogText('[data-test="confirm-body"]')]).toEqual([
      'Bereich Halle löschen',
      'Die Tische in diesem Bereich werden mit gelöscht.',
    ])
    expect(laptop.writes()).toEqual([])
  })

  it('deletes the area once the admin confirms', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await clickOn('[data-test="delete-area"]')

    await pressInDialog('[data-test="confirm"]')

    expect(laptop.writes()).toEqual([{ url: `${AREAS_PATH}/area-halle`, method: 'DELETE', body: undefined }])
  })

  it('shows the reason under the area when the festival is running', async () => {
    editorLaptop({ writeReply: refusal('errors.admin.festivals.areaStaysWhileTheFestivalRuns') })
    await mountEditor()
    await clickOn('[data-test="delete-area"]')

    await pressInDialog('[data-test="confirm"]')

    await waitUntil(() =>
      expect(areaRow('area-halle').querySelector('[data-test="refusal"]')?.textContent?.trim()).toBe(
        'Solange das Fest aktiv ist, kann ein Bereich nicht gelöscht werden. Blenden Sie ihn stattdessen aus.',
      ),
    )
  })
})
