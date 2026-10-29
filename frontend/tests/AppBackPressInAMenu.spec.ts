import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import {
  aBrowserThatCountsTheTap,
  aBrowserWithoutUserActivation,
  aTabThatOpenedTheAppAt,
  pressBack,
  shownOnAStartedRouterAt,
  theAppWasLeftByBack,
  theNextMoveThroughTheHistory,
} from './support/browserHistory'
import { clickOn, isVisibleOnScreen, waitUntil } from './support/dom'
import { anAdminArea, anAdminStation, anAdminTable } from './support/wireViews'

enableAutoUnmount(afterEach)

const KUECHE = anAdminStation({ stationId: 'station-kueche', name: 'Küche' })
const BAR = anAdminStation({ stationId: 'station-bar', name: 'Bar', sortOrder: 2 })
const HALLE_WITH_A_HIDDEN_TABLE = anAdminArea({
  areaId: 'area-halle',
  tables: [anAdminTable({ tableId: 'table-7', name: '7', isHidden: true })],
})
const HALLE_WITHOUT_A_HIDDEN_TABLE = anAdminArea({
  areaId: 'area-halle',
  tables: [anAdminTable({ tableId: 'table-7', name: '7', isHidden: false })],
})
const HALLE_WITH_ANOTHER_HIDDEN_TABLE = anAdminArea({
  areaId: 'area-halle',
  tables: [anAdminTable({ tableId: 'table-7', name: '7', isHidden: false }), anAdminTable({ tableId: 'table-8', name: '8', isHidden: true })],
})

async function openTheMenuOf(activator: string, content: string): Promise<void> {
  await clickOn(activator)
  await waitUntil(() => expect(isVisibleOnScreen(content)).toBe(true))
}

beforeEach(() => {
  vi.resetModules()
  setActivePinia(createPinia())
  document.body.innerHTML = ''
  aBrowserThatCountsTheTap()
})

afterEach(() => {
  aBrowserWithoutUserActivation()
})

describe('back while the station choice of an article is open', () => {
  it('closes the choice and stays on the festival page', async () => {
    await shownOnAStartedRouterAt('/admin/festivals/fest-1', () => import('../src/admin/components/festivals/StationSelect.vue'), {
      stations: [KUECHE, BAR],
      selectedStationIds: [],
      errorText: null,
    })
    await openTheMenuOf('[data-test="station-select"]', '[data-test="station-checkbox"]')

    await pressBack()

    expect({ open: isVisibleOnScreen('[data-test="station-checkbox"]'), leftTheApp: theAppWasLeftByBack() }).toEqual({
      open: false,
      leftTheApp: false,
    })
  })
})

describe('back while a menu of the table plan editor is open', () => {
  it('closes the stations to place and stays in the editor', async () => {
    await shownOnAStartedRouterAt('/admin/festivals/fest-1/areas/area-halle', () => import('../src/admin/components/tablePlan/TablePlanToolbar.vue'), {
      area: HALLE_WITH_A_HIDDEN_TABLE,
      stationsOnNoPlan: [BAR],
    })
    await openTheMenuOf('[data-test="add-station"]', '[data-test="station-to-place"]')

    await pressBack()

    expect({ open: isVisibleOnScreen('[data-test="station-to-place"]'), leftTheApp: theAppWasLeftByBack() }).toEqual({
      open: false,
      leftTheApp: false,
    })
  })

  it('closes the hidden tables and stays in the editor', async () => {
    await shownOnAStartedRouterAt('/admin/festivals/fest-1/areas/area-halle', () => import('../src/admin/components/tablePlan/TablePlanToolbar.vue'), {
      area: HALLE_WITH_A_HIDDEN_TABLE,
      stationsOnNoPlan: [BAR],
    })
    await openTheMenuOf('[data-test="hidden-tables"]', '[data-test="hidden-table"]')

    await pressBack()

    expect({ open: isVisibleOnScreen('[data-test="hidden-table"]'), leftTheApp: theAppWasLeftByBack() }).toEqual({
      open: false,
      leftTheApp: false,
    })
  })

  it('takes its step away again when a station is picked from it, so the next back leaves the editor', async () => {
    await shownOnAStartedRouterAt('/admin/festivals/fest-1/areas/area-halle', () => import('../src/admin/components/tablePlan/TablePlanToolbar.vue'), {
      area: HALLE_WITH_A_HIDDEN_TABLE,
      stationsOnNoPlan: [BAR],
    })
    await openTheMenuOf('[data-test="add-station"]', '[data-test="station-to-place"]')
    const theMenusStepIsGone = theNextMoveThroughTheHistory()
    await clickOn('[data-test="station-to-place"][data-test-id="station-bar"]')
    await theMenusStepIsGone

    await pressBack()

    expect(theAppWasLeftByBack()).toBe(true)
  })
})

describe('the hidden tables of the table plan editor', () => {
  it('stay closed when another table is hidden after the last hidden one was shown while the list was open', async () => {
    const toolbar = await shownOnAStartedRouterAt('/admin/festivals/fest-1/areas/area-halle', () => import('../src/admin/components/tablePlan/TablePlanToolbar.vue'), {
      area: HALLE_WITH_A_HIDDEN_TABLE,
      stationsOnNoPlan: [BAR],
    })
    await openTheMenuOf('[data-test="hidden-tables"]', '[data-test="hidden-table"]')
    await toolbar.setProps({ area: HALLE_WITHOUT_A_HIDDEN_TABLE })

    await toolbar.setProps({ area: HALLE_WITH_ANOTHER_HIDDEN_TABLE })
    await flushPromises()

    expect(isVisibleOnScreen('[data-test="hidden-table"]')).toBe(false)
  })
})

describe('back while the menu of a shape in the table plan editor is open', () => {
  it('closes the menu and stays in the editor', async () => {
    aTabThatOpenedTheAppAt('/admin/festivals/fest-1/areas/area-halle')
    const { startRouter } = await import('../src/shared/router/router')
    startRouter()
    const editor = await import('./admin/components/tablePlan/tablePlanEditorFixture')
    editor.editorLaptop()
    await editor.mountEditor()
    await editor.openTheMenuOf(editor.TABLE_A1_SHAPE_ID)

    await pressBack()

    expect({ open: isVisibleOnScreen('[data-test="table-plan-menu"]'), leftTheApp: theAppWasLeftByBack() }).toEqual({
      open: false,
      leftTheApp: false,
    })
  })

  it('keeps the dialog a menu entry opened, so back closes that dialog and stays in the editor', async () => {
    aTabThatOpenedTheAppAt('/admin/festivals/fest-1/areas/area-halle')
    const { startRouter } = await import('../src/shared/router/router')
    startRouter()
    const editor = await import('./admin/components/tablePlan/tablePlanEditorFixture')
    editor.editorLaptop()
    await editor.mountEditor()
    await editor.chooseInTheMenuOf(editor.TABLE_A1_SHAPE_ID, 'edit-table-from-menu')
    await waitUntil(() => expect(isVisibleOnScreen('[data-test="form-dialog"]')).toBe(true))

    await pressBack()

    expect({ dialog: isVisibleOnScreen('[data-test="form-dialog"]'), leftTheApp: theAppWasLeftByBack() }).toEqual({
      dialog: false,
      leftTheApp: false,
    })
  })
})
