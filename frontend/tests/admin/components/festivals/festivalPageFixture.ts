import { expect, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { VAutocomplete, VCheckbox } from 'vuetify/components'
import FestivalPage from '../../../../src/admin/components/festivals/FestivalPage.vue'
import FestivalPlacementDialog from '../../../../src/admin/components/festivals/FestivalPlacementDialog.vue'
import StationSelect from '../../../../src/admin/components/festivals/StationSelect.vue'
import { AdminItemView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { answer, stubLaptop, type LaptopCall, type LaptopReply, type StubbedLaptop } from '../../../support/laptop'
import { onScreen } from '../../../support/dom'
import { aFestival, anAdminStation } from '../../../support/wireViews'

export const FESTIVAL_ID = 'fest-1'

export const KITCHEN_ID = 'station-kueche'

export const BAR_ID = 'station-theke'

export const SAUSAGE_ID = 'item-bratwurst'

export const BEER_ID = 'item-bier'

export const FOOD_ID = 'category-speisen'

export const SUMMER = aFestival({ festivalId: FESTIVAL_ID })

export const KITCHEN = anAdminStation({ stationId: KITCHEN_ID })

export const BAR = anAdminStation({
  stationId: BAR_ID,
  name: 'Theke',
  sortOrder: 2,
  hasDevice: false,
  isAtAnyFestival: false,
})

export const FOOD = {
  categoryId: FOOD_ID,
  name: 'Speisen',
  colourHex: '#FFEB3B',
  sortOrder: 1,
  isActive: true,
}

export const SAUSAGE = {
  itemId: SAUSAGE_ID,
  name: 'Bratwurst',
  categoryId: FOOD_ID,
  sortOrder: 1,
  isActive: true,
  productionMinutes: null,
  isQueueIndependent: false,
  ingredients: [],
  atTheFestival: { priceCents: 350, isAvailable: true, stationIds: [KITCHEN_ID] },
}

export const DRINKS_ID = 'category-getraenke'

export const DRINKS = {
  categoryId: DRINKS_ID,
  name: 'Getränke',
  colourHex: '#90CAF9',
  sortOrder: 2,
  isActive: true,
}

export const BEER = {
  itemId: BEER_ID,
  name: 'Bier',
  categoryId: FOOD_ID,
  sortOrder: 2,
  isActive: true,
  productionMinutes: null,
  isQueueIndependent: false,
  ingredients: [],
  atTheFestival: null,
}

export interface FestivalScenario {
  festivals?: unknown[]
  stations?: unknown[]
  categories?: unknown[]
  items?: unknown[]
  refusal?: { status: number; body: unknown; method: string }
  refuses?: (call: LaptopCall) => { status: number; body: unknown } | null
  created?: Record<string, unknown>
  waitBeforeAnswering?: (call: LaptopCall) => Promise<void>
  stock?: unknown[]
}

export function festivalLaptop(scenario: FestivalScenario = {}): StubbedLaptop {
  const afterTheWait =
    (reply: LaptopReply): LaptopReply =>
    async (call) => {
      await scenario.waitBeforeAnswering?.(call)
      return reply(call)
    }
  const refusedByMethod = scenario.refusal
  return stubLaptop()
    .answersEverythingElse(afterTheWait(answer({ items: scenario.items ?? [SAUSAGE, BEER] })))
    .answers('GET', '/api/admin/categories', afterTheWait(answer({ categories: scenario.categories ?? [FOOD] })))
    .answers('GET', '/api/admin/stations', afterTheWait(answer({ stations: scenario.stations ?? [KITCHEN, BAR] })))
    .answers('GET', '/api/admin/festivals', afterTheWait(answer({ festivals: scenario.festivals ?? [SUMMER] })))
    .answers('GET', /\/ingredients$/, afterTheWait(answer({ ingredients: scenario.stock ?? [] })))
    .answers('ANY', (call) => call.method !== 'GET', afterTheWait(answer(scenario.created ?? {})))
    .answers(
      'ANY',
      (call) => refusedByMethod !== undefined && call.method === refusedByMethod.method,
      afterTheWait(answer(refusedByMethod?.body, refusedByMethod?.status)),
    )
    .answers(
      'ANY',
      (call) => (scenario.refuses?.(call) ?? null) !== null,
      afterTheWait(async (call) => {
        const refused = scenario.refuses?.(call)
        return answer(refused?.body, refused?.status)(call)
      }),
    )
}

export function mountPage(festivalId = FESTIVAL_ID) {
  return mount(FestivalPage, {
    props: { festivalId },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

export async function openPlacementDialog(page: VueWrapper): Promise<void> {
  await page.getComponent<typeof VAutocomplete>('[data-test="item-search"]').setValue(BEER_ID)
  await page.get('[data-test="add-item"]').trigger('click')
  await vi.waitFor(() =>
    expect(document.querySelector('[data-test="form-dialog-title"]')?.textContent).toContain('Bier'),
  )
}

export function itemRow(page: VueWrapper, itemId: string) {
  return page.get(`[data-test="festival-item-row"][data-test-id="${itemId}"]`)
}

export function stationRow(page: VueWrapper, stationId: string) {
  return page.get(`[data-test="festival-station-row"][data-test-id="${stationId}"]`)
}

export function ingredientRow(page: VueWrapper, ingredientId: string) {
  return page.get(`[data-test="festival-ingredient-row"][data-test-id="${ingredientId}"]`)
}

export function stationBox(stations: VueWrapper, stationId: string): VueWrapper {
  return stations.getComponent<typeof VCheckbox>(
    `[data-test="station-checkbox"][data-test-id="${stationId}"]`,
  )
}

export async function openRowStations(page: VueWrapper, itemId = SAUSAGE_ID): Promise<VueWrapper> {
  const row = itemRow(page, itemId)
  await row.get('[data-test="station-select"]').trigger('click')
  const select = row.getComponent(StationSelect)
  await vi.waitFor(() =>
    expect(select.findComponent('[data-test="station-checkbox"]').exists()).toBe(true),
  )
  return select
}

export async function openDialogStations(page: VueWrapper): Promise<VueWrapper> {
  onScreen('[data-test="form-dialog"] [data-test="station-select"]').click()
  const select = page.findComponent(FestivalPlacementDialog).findComponent(StationSelect)
  await vi.waitFor(() =>
    expect(select.findComponent('[data-test="station-checkbox"]').exists()).toBe(true),
  )
  return select
}

export function listedItems(): AdminItemView[] {
  return [
    {
      ...SAUSAGE,
      atTheFestival: { ...SAUSAGE.atTheFestival, stationIds: [...SAUSAGE.atTheFestival.stationIds] },
    },
    { ...BEER },
  ]
}
