import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { DOMWrapper, enableAutoUnmount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import {
  aBrowserThatCountsTheTap,
  aBrowserWithoutUserActivation,
  pressBack,
  shownOnAStartedRouterAt,
  theAppWasLeftByBack,
  theNextMoveThroughTheHistory,
} from './support/browserHistory'
import { clickOn, waitUntil } from './support/dom'
import { anAdminArea, anAdminStation, anAdminTable } from './support/wireViews'

enableAutoUnmount(afterEach)

const KUECHE = anAdminStation({ stationId: 'station-kueche', name: 'Küche' })
const BAR = anAdminStation({ stationId: 'station-bar', name: 'Bar', sortOrder: 2 })
const HALLE_WITH_A_HIDDEN_TABLE = anAdminArea({
  areaId: 'area-halle',
  tables: [anAdminTable({ tableId: 'table-7', name: '7', isHidden: true })],
})

function theMenuShows(selector: string): boolean {
  const element = document.querySelector(selector)
  return element !== null && new DOMWrapper(element).isVisible()
}

async function openTheMenuOf(activator: string, content: string): Promise<void> {
  await clickOn(activator)
  await waitUntil(() => expect(theMenuShows(content)).toBe(true))
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

    expect({ open: theMenuShows('[data-test="station-checkbox"]'), leftTheApp: theAppWasLeftByBack() }).toEqual({
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

    expect({ open: theMenuShows('[data-test="station-to-place"]'), leftTheApp: theAppWasLeftByBack() }).toEqual({
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

    expect({ open: theMenuShows('[data-test="hidden-table"]'), leftTheApp: theAppWasLeftByBack() }).toEqual({
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
