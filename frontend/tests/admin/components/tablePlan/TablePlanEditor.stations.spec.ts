import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount, type VueWrapper } from '@vue/test-utils'
import { VAutocomplete } from 'vuetify/components'
import {
  allOnScreen,
  clickOn,
  dialogText,
  isOnScreen,
  onScreen,
  pressInDialog,
  waitForDialog,
  waitUntil,
} from '../../../support/dom'
import { answer, inTurn, noConnection } from '../../../support/laptop'
import {
  HALL,
  KITCHEN,
  SCHANK,
  SCHANK_SHAPE,
  SCHANK_SHAPE_ID,
  TENT,
  editorLaptop,
  mountEditor,
  selectTheShape,
  shapesWithTheHandles,
  stationRow,
} from './tablePlanEditorFixture'
import { anAdminStation, anAdminStationShape } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

const SHAPE_PATH = '/api/admin/festivals/fest-1/stations'

const BAR = anAdminStation({ stationId: 'station-theke', name: 'Theke', sortOrder: 3, isAtAnyFestival: true })

const NOT_AT_THE_FESTIVAL = anAdminStation({
  stationId: 'station-grill',
  name: 'Grill',
  sortOrder: 4,
  isAtAnyFestival: false,
})

const TENT_WITH_THE_BAR = {
  ...TENT,
  stationShapes: [anAdminStationShape({ stationId: 'station-theke', stationName: 'Theke' })],
}

function stationSearchOf(editor: VueWrapper) {
  return editor.getComponent<typeof VAutocomplete>('[data-test="station-search"]')
}

async function putTheKitchenHere(editor: VueWrapper): Promise<void> {
  await stationSearchOf(editor).setValue('station-kueche')
  await clickOn('[data-test="add-station"]')
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('the stations beside the drawing', () => {
  it('list the stations on this plan as rows', async () => {
    editorLaptop({ areas: [HALL, TENT_WITH_THE_BAR], stations: [KITCHEN, SCHANK, BAR] })

    await mountEditor()

    expect(allOnScreen('[data-test="station-row"] [data-test="station-name"]').map((name) => name.textContent?.trim())).toEqual([
      'Schank',
    ])
  })

  it('offer to add only the stations of the festival that are on no plan', async () => {
    editorLaptop({ areas: [HALL, TENT_WITH_THE_BAR], stations: [KITCHEN, SCHANK, BAR, NOT_AT_THE_FESTIVAL] })

    const editor = await mountEditor()

    expect((stationSearchOf(editor).props('items') as { name: string }[]).map((station) => station.name)).toEqual([
      'Küche',
    ])
  })

  it('highlight the row of the station the admin taps in the drawing', async () => {
    editorLaptop()
    await mountEditor()

    await selectTheShape(SCHANK_SHAPE_ID)

    expect(stationRow('station-schank').classList.contains('selected-row')).toBe(true)
  })

  it('bring the row of the station the admin taps in the drawing into view', async () => {
    const scrolledIntoView = vi.spyOn(Element.prototype, 'scrollIntoView')
    editorLaptop()
    await mountEditor()

    await selectTheShape(SCHANK_SHAPE_ID)

    expect(scrolledIntoView.mock.contexts).toContain(stationRow('station-schank'))
  })

  it('say that every station of the festival is already in an area when nothing is left to add', async () => {
    editorLaptop({ areas: [HALL, TENT_WITH_THE_BAR], stations: [SCHANK, BAR] })

    const editor = await mountEditor()

    expect(stationSearchOf(editor).props('noDataText')).toBe('Alle Ausgabestellen dieses Festes stehen schon in einem Bereich.')
  })

  it('say that the festival has no station yet when none belongs to it', async () => {
    editorLaptop({ areas: [{ ...HALL, stationShapes: [] }, TENT], stations: [NOT_AT_THE_FESTIVAL] })

    const editor = await mountEditor()

    expect(stationSearchOf(editor).props('noDataText')).toBe('Zu diesem Fest gehört noch keine Ausgabestelle.')
  })
})

describe('adding a station to this area', () => {
  it('places the chosen station below the lowest shape of the area', async () => {
    const laptop = editorLaptop()
    const editor = await mountEditor()

    await putTheKitchenHere(editor)

    expect(laptop.writes()).toEqual([
      {
        url: `${SHAPE_PATH}/station-kueche/shape`,
        method: 'PUT',
        body: { areaId: 'area-halle', geometry: { x: 10, y: 80, width: 120, height: 40, rotation: 0 } },
      },
    ])
  })

  it('selects it in the drawing once the laptop has it', async () => {
    const kitchenShape = anAdminStationShape({ stationId: 'station-kueche', stationName: 'Küche' })
    editorLaptop({
      planReply: inTurn(
        answer({ areas: [HALL, TENT] }),
        answer({ areas: [{ ...HALL, stationShapes: [SCHANK_SHAPE, kitchenShape] }, TENT] }),
      ),
    })
    const editor = await mountEditor()

    await putTheKitchenHere(editor)

    await waitUntil(() => expect(shapesWithTheHandles()).toEqual(['station-station-kueche']))
  })

  it('shows the reason under the field when the laptop cannot be reached', async () => {
    editorLaptop({ writeReply: noConnection() })
    const editor = await mountEditor()

    await putTheKitchenHere(editor)

    await waitUntil(() =>
      expect(onScreen('[data-test="add-station-to-plan"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
      ),
    )
  })
})

describe('taking a station off this plan', () => {
  it('is offered as the trash icon of the station rows on the festival page', async () => {
    editorLaptop()

    await mountEditor()

    expect(onScreen('[data-test="remove-station"]', stationRow('station-schank')).querySelector('.mdi-delete')).not.toBeNull()
  })

  it('asks first, the way the festival page asks before removing a station', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await clickOn('[data-test="remove-station"]', stationRow('station-schank'))
    await waitForDialog()

    expect([dialogText('[data-test="confirm-title"]'), dialogText('[data-test="confirm-body"]')]).toEqual([
      'Ausgabestelle entfernen',
      'Die Ausgabestelle wird nur aus diesem Bereich entfernt.',
    ])
    expect(laptop.writes()).toEqual([])
  })

  it('deletes its shape once the admin confirms', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await clickOn('[data-test="remove-station"]', stationRow('station-schank'))

    await pressInDialog('[data-test="confirm"]')

    expect(laptop.writes()).toEqual([
      { url: `${SHAPE_PATH}/station-schank/shape`, method: 'DELETE', body: undefined },
    ])
  })

  it('offers the station to add again once the laptop has taken its shape off', async () => {
    editorLaptop({ planReply: inTurn(answer({ areas: [HALL, TENT] }), answer({ areas: [{ ...HALL, stationShapes: [] }, TENT] })) })
    const editor = await mountEditor()
    await clickOn('[data-test="remove-station"]', stationRow('station-schank'))

    await pressInDialog('[data-test="confirm"]')

    await waitUntil(() => expect(isOnScreen('[data-test="station-row"][data-test-id="station-schank"]')).toBe(false))
    expect((stationSearchOf(editor).props('items') as { name: string }[]).map((station) => station.name)).toEqual([
      'Küche',
      'Schank',
    ])
  })
})
