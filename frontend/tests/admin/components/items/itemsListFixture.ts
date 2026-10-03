import { mount } from '@vue/test-utils'
import ItemsList from '../../../../src/admin/components/items/ItemsList.vue'
import { testPlugins } from '../../../support/plugins'
import { answer, stubLaptop, type LaptopCall, type StubbedLaptop } from '../../../support/laptop'

export const ITEM_ID = '22222222-2222-2222-2222-222222222222'

const STATION_ID = '11111111-1111-1111-1111-111111111111'

export const FOOD_ID = '33333333-3333-3333-3333-333333333333'

const DRINKS_ID = '44444444-4444-4444-4444-444444444444'

const DESSERT_ID = '55555555-5555-5555-5555-555555555555'

export const TWO_CATEGORIES = {
  categories: [
    {
      categoryId: FOOD_ID,
      name: 'Speisen',
      colourHex: '#FFEB3B',
      sortOrder: 1,
      isActive: true,
    },
    {
      categoryId: DRINKS_ID,
      name: 'Getränke',
      colourHex: '#C62828',
      sortOrder: 2,
      isActive: true,
    },
  ],
}

const CREATED_CATEGORY = {
  categoryId: DESSERT_ID,
  name: 'Nachtisch',
  colourHex: '#6D4C41',
  sortOrder: 3,
  isActive: true,
}

export const FESTIVAL_ID = '66666666-6666-6666-6666-666666666666'

export const ONE_FESTIVAL = {
  festivals: [
    {
      festivalId: FESTIVAL_ID,
      name: 'Sommerfest',
      startsAtUtc: '2026-07-18T10:00:00Z',
      endsAtUtc: '2026-07-19T02:00:00Z',
      isHidden: false,
      isRunning: false,
      stationCount: 1,
      menuItemCount: 1,
      orderCount: 0,
    },
  ],
}

export const ONE_ITEM = {
  items: [
    {
      itemId: ITEM_ID,
      name: 'Bratwurst',
      categoryId: FOOD_ID,
      sortOrder: 1,
      isActive: true,
      productionMinutes: null,
      isQueueIndependent: false,
      ingredients: [],
      atTheFestival: { priceCents: 350, isAvailable: true, stationIds: [STATION_ID] },
    },
  ],
}

export const ONE_STATION = {
  stations: [
    {
      stationId: STATION_ID,
      name: 'Küche',
      sortOrder: 1,
      isActive: true,
      hasDevice: true,
      isAtAnyFestival: true,
    },
  ],
}

export const DEACTIVATED_ITEM = {
  items: [{ ...ONE_ITEM.items[0], isActive: false }],
}

export const THREE_ITEMS = {
  items: [
    { ...ONE_ITEM.items[0], itemId: 'aaaa1111-2222-4333-8444-555566667777', name: 'Wasser', categoryId: DRINKS_ID },
    { ...ONE_ITEM.items[0], itemId: 'bbbb1111-2222-4333-8444-555566667777', name: 'Schnitzel', categoryId: FOOD_ID },
    { ...ONE_ITEM.items[0], itemId: 'cccc1111-2222-4333-8444-555566667777', name: 'Bier', categoryId: DRINKS_ID },
  ],
}

export interface ItemsListScenario {
  items?: unknown
  categories?: unknown
  stations?: unknown
  festivals?: unknown
  refusal?: { status: number; body: unknown }
  itemRefusal?: { status: number; body: unknown }
  ingredients?: unknown
}

export function itemsListLaptop(scenario: ItemsListScenario = {}): StubbedLaptop {
  const toward = (path: string) => (call: LaptopCall) => call.url.includes(path)
  const writingTo = (path: string) => (call: LaptopCall) => call.method !== 'GET' && call.url.includes(path)
  return stubLaptop()
    .answersEverythingElse((call) => answer(scenario.stations ?? ONE_STATION)(call))
    .answers('ANY', toward('/api/admin/items'), (call) => answer(scenario.items ?? ONE_ITEM)(call))
    .answers('ANY', writingTo('/api/admin/items'), (call) =>
      scenario.itemRefusal === undefined
        ? answer(scenario.items ?? ONE_ITEM)(call)
        : answer(scenario.itemRefusal.body, scenario.itemRefusal.status)(call),
    )
    .answers('ANY', toward('/api/admin/festivals'), (call) => answer(scenario.festivals ?? ONE_FESTIVAL)(call))
    .answers('ANY', toward('/api/admin/festivals/'), answer({}))
    .answers('ANY', toward('/api/admin/ingredients'), (call) =>
      answer(scenario.ingredients ?? { ingredients: [] })(call),
    )
    .answers('ANY', toward('/api/admin/categories'), (call) =>
      answer(scenario.categories ?? TWO_CATEGORIES)(call),
    )
    .answers('ANY', writingTo('/api/admin/categories'), (call) =>
      scenario.refusal === undefined
        ? (call.method === 'POST' && call.url === '/api/admin/categories'
            ? answer(CREATED_CATEGORY, 201)(call)
            : answer(scenario.categories ?? TWO_CATEGORIES)(call))
        : answer(scenario.refusal.body, scenario.refusal.status)(call),
    )
}

export function mountList() {
  return mount(ItemsList, { global: { plugins: testPlugins() }, attachTo: document.body })
}
