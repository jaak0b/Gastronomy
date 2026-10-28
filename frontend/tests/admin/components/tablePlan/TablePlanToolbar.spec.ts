import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import TablePlanToolbar from '../../../../src/admin/components/tablePlan/TablePlanToolbar.vue'
import { clickOn, textOnScreen, waitUntil } from '../../../support/dom'
import { testPlugins } from '../../../support/plugins'
import { anAdminArea, anAdminStation, anAdminTable } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

const A1 = anAdminTable({ tableId: 'table-a1', name: 'A1' })

const HIDDEN_A3 = anAdminTable({ tableId: 'table-a3', name: 'A3', isHidden: true })

const KITCHEN = anAdminStation({ stationId: 'station-kueche', name: 'Küche', isAtFestival: true })

function mountToolbar(locale: 'de' | 'en' = 'de') {
  return mount(TablePlanToolbar, {
    props: {
      area: anAdminArea({ tables: [A1, HIDDEN_A3] }),
      stationsOnNoPlan: [KITCHEN],
      festivalHasStations: true,
    },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

beforeEach(() => {
  document.body.innerHTML = ''
})

describe('the toolbar in English', () => {
  it('names its buttons New table, Add a station and Hidden tables', () => {
    mountToolbar('en')

    expect(
      ['new-table', 'add-station', 'hidden-tables'].map((button) => textOnScreen(`[data-test="${button}"]`).trim()),
    ).toEqual(['New table', 'Add a station', 'Hidden tables'])
  })
})

describe('the dropdowns of the toolbar', () => {
  it('hand the editor the station the admin picks', async () => {
    const toolbar = mountToolbar()
    await clickOn('[data-test="add-station"]')
    await waitUntil(() => expect(document.querySelector('[data-test="station-to-place"]')).not.toBeNull())

    await clickOn('[data-test="station-to-place"][data-test-id="station-kueche"]')

    expect(toolbar.emitted('placeStation')).toEqual([['station-kueche']])
  })

  it('hand the editor the hidden table whose eye the admin presses', async () => {
    const toolbar = mountToolbar()
    await clickOn('[data-test="hidden-tables"]')
    await waitUntil(() => expect(document.querySelector('[data-test="show-table"]')).not.toBeNull())

    await clickOn('[data-test="hidden-table"][data-test-id="table-a3"] [data-test="show-table"]')

    expect(toolbar.emitted('showTable')).toEqual([[HIDDEN_A3]])
  })
})
