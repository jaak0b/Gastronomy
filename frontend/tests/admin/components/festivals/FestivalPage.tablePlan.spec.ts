import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import FestivalPage from '../../../../src/admin/components/festivals/FestivalPage.vue'
import { currentRoute } from '../../../../src/shared/router/router'
import {
  allOnScreen,
  clickOn,
  iconActionLabelsOnScreen,
  isOnScreen,
  onScreen,
  refusalReasonsOnScreen,
  textOnScreen,
  waitUntil,
} from '../../../support/dom'
import { aHold, answer, heldUntil } from '../../../support/laptop'
import { testPlugins } from '../../../support/plugins'
import { FESTIVAL_ID, festivalLaptop, mountPage } from './festivalPageFixture'
import { HALL, OLD_TABLES, TENT, areaRow, hideButtonOf } from './festivalTablePlanFixture'
import { anAdminArea, anAdminTable } from '../../../support/wireViews'

enableAutoUnmount(afterEach)

const STILL_OPEN =
  'Solange ein Fest aktiv ist, kann ein Tischplan erst ausgeblendet werden, wenn an seinen Tischen alles zubereitet und kassiert ist.'

const TENT_WITH_ITEMS_TO_HAND_OUT = { ...TENT, hasItemsToHandOutOrSettleAtTheRunningFestival: true }

async function theAreasAreListed(): Promise<void> {
  await vi.waitFor(() => expect(isOnScreen('[data-test="festival-area-row"]')).toBe(true))
}

beforeEach(() => {
  setActivePinia(createPinia())
  window.history.replaceState({}, '', `/admin/festivals/${FESTIVAL_ID}`)
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the areas on the table plan card of the festival page', () => {
  it('are listed by name in the order of the laptop, each with its number of tables', async () => {
    festivalLaptop({ areas: [HALL, TENT] })

    mountPage()
    await theAreasAreListed()

    expect(
      [areaRow('area-halle'), areaRow('area-zelt')].map((row) => [
        textOnScreen('[data-test="entry-name"]', row).trim(),
        textOnScreen('[data-test="table-count"]', row).trim(),
      ]),
    ).toEqual([
      ['Halle', '2 Tische'],
      ['Zelt', '1 Tisch'],
    ])
  })

  it('count only the tables waiters see', async () => {
    const hiddenTable = anAdminTable({ tableId: 'table-b2', name: 'B2', isHidden: true })
    festivalLaptop({ areas: [{ ...TENT, tables: [...TENT.tables, hiddenTable] }] })

    mountPage()
    await theAreasAreListed()

    expect(textOnScreen('[data-test="table-count"]', areaRow('area-zelt')).trim()).toBe('1 Tisch')
  })

  it('leave a hidden area out until the admin asks for hidden areas', async () => {
    festivalLaptop({ areas: [HALL, OLD_TABLES] })

    mountPage()
    await theAreasAreListed()

    expect(allOnScreen('[data-test="festival-area-row"]').map((row) => row.getAttribute('data-test-id'))).toEqual([
      'area-halle',
    ])
  })

  it('show a hidden area, marked hidden and offered to show again, once the box for hidden areas is ticked', async () => {
    festivalLaptop({ areas: [HALL, OLD_TABLES] })
    mountPage()
    await theAreasAreListed()

    await clickOn('[data-test="show-hidden-entries"] input')
    onScreen('[data-test="show-entry"]', areaRow('area-alt')).dispatchEvent(new MouseEvent('mouseenter'))

    expect([
      textOnScreen('[data-test="hidden"]', areaRow('area-alt')).trim(),
      isOnScreen('[data-test="show-entry"] .mdi-eye', areaRow('area-alt')),
    ]).toEqual(['Ausgeblendet', true])
    await waitUntil(() =>
      expect(iconActionLabelsOnScreen()).toContain('Einblenden'),
    )
  })

  it('offer the box for hidden areas in the heading line of the card, beside its title', async () => {
    festivalLaptop({ areas: [HALL] })
    mountPage()
    await theAreasAreListed()

    const headingLine = onScreen('[data-test="section-heading"]', onScreen('[data-test="festival-table-plan"]')).parentElement

    expect(headingLine?.querySelector('[data-test="show-hidden-entries"]')?.textContent?.trim()).toBe(
      'Ausgeblendete anzeigen',
    )
  })

  it('offer opening the plan, the arrows, the pencil and the trash icon in that order', async () => {
    festivalLaptop({ areas: [HALL, TENT] })
    mountPage()
    await theAreasAreListed()

    const actions = allOnScreen(
      '[data-test="open-plan"], [data-test="move-up"], [data-test="move-down"], [data-test="edit-entry"], [data-test="hide-entry"]',
      areaRow('area-zelt'),
    )

    expect(actions.map((action) => action.getAttribute('data-test'))).toEqual([
      'open-plan',
      'move-up',
      'move-down',
      'edit-entry',
      'hide-entry',
    ])
  })

  it('offer editing an area through the pencil, named "Bearbeiten"', async () => {
    festivalLaptop({ areas: [HALL, TENT] })
    mountPage()
    await theAreasAreListed()

    onScreen('[data-test="edit-entry"]', areaRow('area-zelt')).dispatchEvent(new MouseEvent('mouseenter'))

    expect(isOnScreen('[data-test="edit-entry"] .mdi-pencil', areaRow('area-zelt'))).toBe(true)
    await waitUntil(() => expect(iconActionLabelsOnScreen()).toContain('Bearbeiten'))
  })

  it('open the editor of the area whose plan the admin opens', async () => {
    festivalLaptop({ areas: [HALL, TENT] })
    mountPage()
    await theAreasAreListed()

    await clickOn('[data-test="open-plan"]', areaRow('area-zelt'))

    expect(currentRoute.value).toEqual({
      name: 'admin',
      section: 'festivals',
      festivalId: FESTIVAL_ID,
      areaId: 'area-zelt',
    })
  })

  it('count the tables in English too', async () => {
    festivalLaptop({ areas: [TENT] })

    mount(FestivalPage, {
      props: { festivalId: FESTIVAL_ID },
      global: { plugins: testPlugins('en') },
      attachTo: document.body,
    })
    await vi.waitFor(() => expect(isOnScreen('[data-test="table-count"]')).toBe(true))

    expect(textOnScreen('[data-test="table-count"]').trim()).toBe('1 table')
  })
})

describe('hiding an area from the table plan card', () => {
  it('is offered as the trash icon the station rows of the festival page carry', async () => {
    festivalLaptop({ areas: [HALL, TENT] })

    mountPage()
    await theAreasAreListed()

    expect(hideButtonOf('area-zelt').querySelector('.mdi-delete')).not.toBeNull()
  })

  it('cannot be asked for while the festival runs and a table of the area has items to hand out or settle', async () => {
    festivalLaptop({ areas: [HALL, TENT_WITH_ITEMS_TO_HAND_OUT] })

    mountPage()
    await theAreasAreListed()

    expect(hideButtonOf('area-zelt').disabled).toBe(true)
  })

  it('names the reason on the disabled trash icon', async () => {
    festivalLaptop({ areas: [HALL, TENT_WITH_ITEMS_TO_HAND_OUT] })
    mountPage()
    await theAreasAreListed()

    onScreen('[data-test="hide-entry-wrapper"]', areaRow('area-zelt')).dispatchEvent(new MouseEvent('mouseenter'))

    await waitUntil(() =>
      expect(refusalReasonsOnScreen()).toContain(
        STILL_OPEN,
      ),
    )
  })
})

describe('the warnings of the table plan card', () => {
  it('name the button for a new area when the festival has none', async () => {
    festivalLaptop({ areas: [] })

    mountPage()
    await vi.waitFor(() => expect(isOnScreen('[data-test="no-area"]')).toBe(true))

    expect(textOnScreen('[data-test="no-area"]').trim()).toBe(
      'Die Kellner sehen noch keinen Tischplan. Klicken Sie auf „Neuer Tischplan“.',
    )
  })

  it('look like the warning of the stock card, an alert icon beside text in the warning colour', async () => {
    festivalLaptop({ areas: [] })

    mountPage()
    await vi.waitFor(() => expect(isOnScreen('[data-test="no-area"]')).toBe(true))

    const warning = onScreen('[data-test="no-area"]')
    expect([isOnScreen('.mdi-alert', warning), warning.classList.contains('text-warning')]).toEqual([true, true])
  })

  it('stay away while the plan of the festival has not arrived', async () => {
    const thePlan = aHold()
    const laptop = festivalLaptop({ areas: [] }).answers(
      'GET',
      /\/table-plan$/,
      heldUntil(thePlan.released, answer({ areas: [] })),
    )

    mountPage()
    await vi.waitFor(() => expect(isOnScreen('[data-test="festival-name"]')).toBe(true))
    await vi.waitFor(() => expect(laptop.urls()).toContain(`/api/admin/festivals/${FESTIVAL_ID}/table-plan`))

    expect(isOnScreen('[data-test="no-area"]')).toBe(false)
  })

  it('name the button that opens a plan when no area has a table', async () => {
    festivalLaptop({ areas: [anAdminArea({ areaId: 'area-halle', name: 'Halle' })] })

    mountPage()
    await vi.waitFor(() => expect(isOnScreen('[data-test="no-table"]')).toBe(true))

    expect(textOnScreen('[data-test="no-table"]').trim()).toBe(
      'Die Kellner sehen noch keinen Tisch. Klicken Sie bei einem Tischplan auf „Tischplan öffnen“.',
    )
  })

  it('say so when the shown areas hold only hidden tables', async () => {
    const hiddenTable = anAdminTable({ tableId: 'table-a1', name: 'A1', isHidden: true })
    festivalLaptop({ areas: [anAdminArea({ areaId: 'area-halle', name: 'Halle', tables: [hiddenTable] })] })

    mountPage()

    await vi.waitFor(() => expect(isOnScreen('[data-test="no-table"]')).toBe(true))
  })

  it('stay away once a shown area holds a shown table, whatever is hidden beside it', async () => {
    festivalLaptop({ areas: [OLD_TABLES, TENT] })

    mountPage()
    await theAreasAreListed()

    expect([
      isOnScreen('[data-test="no-area"]'),
      isOnScreen('[data-test="no-table"]'),
    ]).toEqual([false, false])
  })
})
