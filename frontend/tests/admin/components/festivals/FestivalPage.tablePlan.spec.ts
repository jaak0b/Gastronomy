import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import FestivalPage from '../../../../src/admin/components/festivals/FestivalPage.vue'
import { currentRoute } from '../../../../src/shared/router/router'
import { testPlugins } from '../../../support/plugins'
import { FESTIVAL_ID, festivalLaptop, mountPage } from './festivalPageFixture'
import { anAdminArea, anAdminTable } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

const HALL = anAdminArea({
  areaId: 'area-halle',
  name: 'Halle',
  tables: [anAdminTable({ tableId: 'table-a1', name: 'A1' }), anAdminTable({ tableId: 'table-a2', name: 'A2' })],
})

const TENT = anAdminArea({
  areaId: 'area-zelt',
  name: 'Zelt',
  sortOrder: 2,
  tables: [anAdminTable({ tableId: 'table-b1', name: 'B1' })],
})

const OLD_TABLES = anAdminArea({ areaId: 'area-alt', name: 'Bisherige Tische', sortOrder: 3, isHidden: true })

function areaRow(areaId: string): HTMLElement | null {
  return document.querySelector(`[data-test="festival-area-row"][data-test-id="${areaId}"]`)
}

beforeEach(() => {
  setActivePinia(createPinia())
  window.history.replaceState({}, '', `/admin/festivals/${FESTIVAL_ID}`)
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the table plan card of the festival page', () => {
  it('lists every area in the order of the laptop with its number of tables', async () => {
    festivalLaptop({ areas: [HALL, TENT] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-area-row"]').exists()).toBe(true))

    expect(
      page.findAll('[data-test="festival-area-row"]').map((row) => [
        row.get('[data-test="area-name"]').text(),
        row.get('[data-test="table-count"]').text(),
      ]),
    ).toEqual([
      ['Halle', '2 Tische'],
      ['Zelt', '1 Tisch'],
    ])
  })

  it('marks a hidden area the way the festival list marks a hidden festival', async () => {
    festivalLaptop({ areas: [HALL, OLD_TABLES] })

    mountPage()
    await vi.waitFor(() => expect(areaRow('area-alt')).not.toBeNull())

    expect(areaRow('area-alt')?.querySelector('[data-test="hidden"]')?.textContent?.trim()).toBe('Ausgeblendet')
    expect(areaRow('area-halle')?.querySelector('[data-test="hidden"]')).toBeNull()
  })

  it('warns when the festival has no area yet', async () => {
    festivalLaptop({ areas: [] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="no-area"]').exists()).toBe(true))

    expect(page.get('[data-test="no-area"]').text()).toBe(
      'Die Kellner sehen noch keinen Bereich. Klicken Sie auf "Tischplan bearbeiten".',
    )
  })

  it('warns when the only area is hidden, because the waiters do not see it', async () => {
    festivalLaptop({ areas: [{ ...OLD_TABLES, tables: [anAdminTable({ tableId: 'table-12', name: '12' })] }] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-area-row"]').exists()).toBe(true))

    expect(page.find('[data-test="no-area"]').exists()).toBe(true)
  })

  it('warns when none of its areas has a table', async () => {
    festivalLaptop({ areas: [anAdminArea({ areaId: 'area-halle', name: 'Halle' })] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="no-table"]').exists()).toBe(true))

    expect(page.get('[data-test="no-table"]').text()).toBe(
      'Die Kellner sehen noch keinen Tisch. Klicken Sie auf "Tischplan bearbeiten".',
    )
  })

  it('warns when the shown areas hold only hidden tables', async () => {
    const hiddenTable = anAdminTable({ tableId: 'table-a1', name: 'A1', isHidden: true })
    festivalLaptop({ areas: [anAdminArea({ areaId: 'area-halle', name: 'Halle', tables: [hiddenTable] })] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-area-row"]').exists()).toBe(true))

    expect(page.find('[data-test="no-table"]').exists()).toBe(true)
  })

  it('shows no warning once a shown area holds a shown table, whatever is hidden beside it', async () => {
    festivalLaptop({ areas: [OLD_TABLES, TENT] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-area-row"]').exists()).toBe(true))

    expect(page.find('[data-test="no-area"]').exists()).toBe(false)
    expect(page.find('[data-test="no-table"]').exists()).toBe(false)
  })

  it('opens the table plan editor of this festival', async () => {
    festivalLaptop({ areas: [HALL] })
    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="edit-table-plan"]').exists()).toBe(true))

    await page.get('[data-test="edit-table-plan"]').trigger('click')

    expect(currentRoute.value).toEqual({
      name: 'admin',
      section: 'festivals',
      festivalId: FESTIVAL_ID,
      festivalSubpage: 'tablePlan',
    })
  })

  it('counts the tables in English too', async () => {
    festivalLaptop({ areas: [TENT] })

    const page = mount(FestivalPage, {
      props: { festivalId: FESTIVAL_ID },
      global: { plugins: testPlugins('en') },
      attachTo: document.body,
    })
    await vi.waitFor(() => expect(page.find('[data-test="table-count"]').exists()).toBe(true))

    expect(page.get('[data-test="table-count"]').text()).toBe('1 table')
  })
})
