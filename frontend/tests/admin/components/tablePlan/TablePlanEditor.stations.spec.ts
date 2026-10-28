import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount } from '@vue/test-utils'
import {
  allOnScreen,
  clickOn,
  dialogText,
  isOnScreen,
  pressInDialog,
  textOnScreen,
  waitForDialog,
  waitUntil,
} from '../../../support/dom'
import type Konva from 'konva'
import { hasShapeWithId, shapeWithId } from '../../../support/konvaDrawing'
import { answer, inTurn, noConnection } from '../../../support/laptop'
import {
  HALL,
  KITCHEN,
  SCHANK,
  SCHANK_SHAPE,
  SCHANK_SHAPE_ID,
  TABLE_A1,
  TENT,
  editorLaptop,
  isHighlighted,
  mountEditor,
  chooseInTheMenuOf,
  openTheMenuOf,
  openTheToolbarMenu,
  planRefusalText,
  selectTheShape,
  shapesWithTheHandles,
  theDrawing,
} from './tablePlanEditorFixture'
import { aGeometry, anAdminStation, anAdminStationShape, anAdminTable } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

const SHAPE_PATH = '/api/admin/festivals/fest-1/stations'

const BAR = anAdminStation({ stationId: 'station-theke', name: 'Theke', sortOrder: 3, isAtFestival: true })

const NOT_AT_THE_FESTIVAL = anAdminStation({
  stationId: 'station-grill',
  name: 'Grill',
  sortOrder: 4,
  isAtFestival: false,
})

const TENT_WITH_THE_BAR = {
  ...TENT,
  stationShapes: [anAdminStationShape({ stationId: 'station-theke', stationName: 'Theke' })],
}

function stationsOffered(): string[] {
  return allOnScreen('[data-test="station-to-place"]').map((entry) => entry.textContent?.trim() ?? '')
}

async function putTheKitchenHere(): Promise<void> {
  await openTheToolbarMenu('add-station', 'station-to-place')
  await clickOn('[data-test="station-to-place"][data-test-id="station-kueche"]')
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

describe('the station dropdown of the toolbar', () => {
  it('offers only the stations of the festival that are on no plan', async () => {
    editorLaptop({ areas: [HALL, TENT_WITH_THE_BAR], stations: [KITCHEN, SCHANK, BAR, NOT_AT_THE_FESTIVAL] })
    await mountEditor()

    await openTheToolbarMenu('add-station', 'station-to-place')

    expect(stationsOffered()).toEqual(['Küche'])
  })

  it('says that every station of the festival is already on a table plan when nothing is left to add', async () => {
    editorLaptop({ areas: [HALL, TENT_WITH_THE_BAR], stations: [SCHANK, BAR] })
    await mountEditor()

    await openTheToolbarMenu('add-station', 'no-station-to-place')

    expect(textOnScreen('[data-test="no-station-to-place"]').trim()).toBe(
      'Alle Ausgabestellen dieses Festes stehen schon in einem Tischplan.',
    )
  })

  it('says that the festival has no station yet when none belongs to it', async () => {
    editorLaptop({ areas: [{ ...HALL, stationShapes: [] }, TENT], stations: [NOT_AT_THE_FESTIVAL] })
    await mountEditor()

    await openTheToolbarMenu('add-station', 'no-station-to-place')

    expect(textOnScreen('[data-test="no-station-to-place"]').trim()).toBe('Zu diesem Fest gehört noch keine Ausgabestelle.')
  })
})

describe('adding a station to this area', () => {
  it('places the chosen station below the lowest shape of the area', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await putTheKitchenHere()

    expect(laptop.writes()).toEqual([
      {
        url: `${SHAPE_PATH}/station-kueche/shape`,
        method: 'PUT',
        body: { areaId: 'area-halle', geometry: { x: 10, y: 80, width: 120, height: 40, rotation: 0 } },
      },
    ])
  })

  it('goes below a hidden table of the area too, the way a new table does', async () => {
    const hiddenA2 = anAdminTable({ tableId: 'table-a2', name: 'A2', isHidden: true, geometry: aGeometry({ x: 10, y: 80 }) })
    const laptop = editorLaptop({ areas: [{ ...HALL, tables: [TABLE_A1, hiddenA2] }, TENT] })
    await mountEditor()

    await putTheKitchenHere()

    expect(laptop.writtenBodies()).toEqual([
      { areaId: 'area-halle', geometry: { x: 10, y: 130, width: 120, height: 40, rotation: 0 } },
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
    await mountEditor()

    await putTheKitchenHere()

    await waitUntil(() => expect(shapesWithTheHandles()).toEqual(['station-station-kueche']))
  })

  it('shows the reason above the drawing when the laptop cannot be reached', async () => {
    editorLaptop({ writeReply: noConnection() })
    await mountEditor()

    await putTheKitchenHere()

    await waitUntil(() =>
      expect(planRefusalText()).toBe(
        'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
      ),
    )
  })
})

describe('a station the admin taps in the drawing', () => {
  it('is highlighted', async () => {
    editorLaptop()
    await mountEditor()

    await selectTheShape(SCHANK_SHAPE_ID)

    expect(isHighlighted(SCHANK_SHAPE_ID)).toBe(true)
  })

  it('gets the border of a selected table, in the same colour and width', async () => {
    editorLaptop()
    await mountEditor()

    await selectTheShape(SCHANK_SHAPE_ID)

    const border = shapeWithId(theDrawing(), SCHANK_SHAPE_ID).findOne<Konva.Rect>('Rect')
    expect([border?.stroke(), border?.strokeWidth()]).toEqual(['#1f2937', 3])
  })

})

describe('the context menu on a station', () => {
  it('offers only Entfernen, with the trash icon before the text', async () => {
    editorLaptop()
    await mountEditor()

    await openTheMenuOf(SCHANK_SHAPE_ID)

    expect(
      allOnScreen('[data-test="table-plan-menu"] .v-list-item').map((entry) => [
        isOnScreen('.v-list-item__prepend .mdi-delete', entry),
        entry.querySelector('.v-list-item-title')?.textContent?.trim(),
      ]),
    ).toEqual([[true, 'Entfernen']])
  })

  it('selects the station it was opened on', async () => {
    editorLaptop()
    await mountEditor()

    await openTheMenuOf(SCHANK_SHAPE_ID)

    expect(isHighlighted(SCHANK_SHAPE_ID)).toBe(true)
  })
})

describe('taking a station off this plan from its menu', () => {
  it('asks first, the way the festival page asks before removing a station', async () => {
    const laptop = editorLaptop()
    await mountEditor()

    await chooseInTheMenuOf(SCHANK_SHAPE_ID, 'remove-station-from-menu')
    await waitForDialog()

    expect([dialogText('[data-test="confirm-title"]'), dialogText('[data-test="confirm-body"]')]).toEqual([
      'Ausgabestelle entfernen',
      'Die Ausgabestelle wird nur aus diesem Tischplan entfernt.',
    ])
    expect(laptop.writes()).toEqual([])
  })

  it('deletes its shape once the admin confirms', async () => {
    const laptop = editorLaptop()
    await mountEditor()
    await chooseInTheMenuOf(SCHANK_SHAPE_ID, 'remove-station-from-menu')

    await pressInDialog('[data-test="confirm"]')

    expect(laptop.writes()).toEqual([
      { url: `${SHAPE_PATH}/station-schank/shape`, method: 'DELETE', body: undefined },
    ])
  })

  it('offers the station to add again once the laptop has taken its shape off', async () => {
    editorLaptop({ planReply: inTurn(answer({ areas: [HALL, TENT] }), answer({ areas: [{ ...HALL, stationShapes: [] }, TENT] })) })
    await mountEditor()
    await chooseInTheMenuOf(SCHANK_SHAPE_ID, 'remove-station-from-menu')
    await pressInDialog('[data-test="confirm"]')
    await waitUntil(() => expect(hasShapeWithId(theDrawing(), SCHANK_SHAPE_ID)).toBe(false))

    await openTheToolbarMenu('add-station', 'station-to-place')

    expect(stationsOffered()).toEqual(['Küche', 'Schank'])
  })
})
