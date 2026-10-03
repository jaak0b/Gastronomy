import { defineComponent } from 'vue'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import StationPage from '../../../src/station/views/StationPage.vue'
import { useLocaleBinding } from '../../../src/shared/composables/useLocaleBinding'
import { useSessionStore } from '../../../src/shared/stores/session'
import { testPlugins } from '../../support/plugins'
import { stubLaptopAt, answer, type LaptopReply, type StubbedLaptop } from '../../support/laptop'

export const KITCHEN = { id: 'station-kueche', name: 'Küche' }

export const TOGETHER_STATION_ORDER = {
  stationOrderId: 'station-order-1',
  globalOrderNumber: 137,
  stationOrderNumber: 12,
  tableName: '3',
  staffMemberName: 'Anna',
  deliveryMode: 'together',
  createdAtUtc: '2026-09-05T18:00:00Z',
  isHiddenFromAsItComesQueue: false,
  itemCount: 3,
  fulfilledItemCount: 1,
  items: [
    { orderItemId: 'a', itemName: 'Bratwurst', note: 'ohne Senf', fulfilledAtUtc: null },
    { orderItemId: 'b', itemName: 'Pommes', note: null, fulfilledAtUtc: null },
    {
      orderItemId: 'c',
      itemName: 'Bratwurst',
      note: null,
      fulfilledAtUtc: '2026-09-05T18:10:00Z',
    },
  ],
}

export const AS_IT_COMES_STATION_ORDER = {
  stationOrderId: 'station-order-2',
  globalOrderNumber: 138,
  stationOrderNumber: 14,
  tableName: '7',
  staffMemberName: 'Ben',
  deliveryMode: 'asItComes',
  createdAtUtc: '2026-09-05T18:05:00Z',
  isHiddenFromAsItComesQueue: false,
  itemCount: 2,
  fulfilledItemCount: 0,
  items: [
    { orderItemId: 'd', itemName: 'Bratwurst', note: null, fulfilledAtUtc: null },
    { orderItemId: 'e', itemName: 'Bier', note: null, fulfilledAtUtc: null },
  ],
}

export const NOTED_STATION_ORDER = {
  stationOrderId: 'station-order-3',
  globalOrderNumber: 140,
  stationOrderNumber: 16,
  tableName: '5',
  staffMemberName: 'Clara',
  deliveryMode: 'together',
  createdAtUtc: '2026-09-05T18:07:00Z',
  isHiddenFromAsItComesQueue: false,
  itemCount: 2,
  fulfilledItemCount: 0,
  items: [
    { orderItemId: 'f', itemName: 'Frankfurter', note: 'Mit Ketchup', fulfilledAtUtc: null },
    { orderItemId: 'g', itemName: 'Frankfurter', note: 'Ohne Ketchup', fulfilledAtUtc: null },
  ],
}

export const DONE_STATION_ORDER = {
  ...TOGETHER_STATION_ORDER,
  itemCount: 3,
  fulfilledItemCount: 2,
  items: [
    { orderItemId: 'a', itemName: 'Bratwurst', note: null, fulfilledAtUtc: '2026-09-05T19:00:00Z' },
    { orderItemId: 'b', itemName: 'Pommes', note: null, fulfilledAtUtc: null },
    { orderItemId: 'c', itemName: 'Bratwurst', note: null, fulfilledAtUtc: '2026-09-05T18:10:00Z' },
  ],
}

export const DONE_NOTED_STATION_ORDER = {
  ...NOTED_STATION_ORDER,
  itemCount: 2,
  fulfilledItemCount: 2,
  items: [
    {
      orderItemId: 'f',
      itemName: 'Frankfurter',
      note: 'Mit Ketchup',
      fulfilledAtUtc: '2026-09-05T19:00:00Z',
    },
    {
      orderItemId: 'g',
      itemName: 'Frankfurter',
      note: 'Ohne Ketchup',
      fulfilledAtUtc: '2026-09-05T19:01:00Z',
    },
  ],
}

export function queue(orders: unknown[], asItComes: unknown[] = []): LaptopReply {
  return answer({ station: KITCHEN, orders, asItComes })
}

export function queueWithBoard(): LaptopReply {
  return queue([TOGETHER_STATION_ORDER, AS_IT_COMES_STATION_ORDER], [AS_IT_COMES_STATION_ORDER])
}

export interface StubRoutes {
  orders?: LaptopReply
  fulfilled?: LaptopReply
  fulfill?: LaptopReply
  unfulfill?: LaptopReply
  hide?: LaptopReply
}

export function stationLaptop(routes: StubRoutes = {}): StubbedLaptop {
  return stubLaptopAt({
    '/api/station/orders': routes.orders ?? queueWithBoard(),
    '/api/station/orders/fulfilled': routes.fulfilled ?? answer({ stationOrders: [] }),
    '/api/station/items/fulfill': routes.fulfill ?? queue([]),
    '/api/station/items/unfulfill': routes.unfulfill ?? queue([]),
  }).answers('ANY', /\/hide$/, routes.hide ?? queue([]))
}

export async function mountPage(): Promise<VueWrapper> {
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.station = KITCHEN
  const page = mount(StationPage, {
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
  await flushPromises()
  return page
}

export const StationPageFollowingTheLanguage = defineComponent({
  components: { StationPage },
  setup() {
    const session = useSessionStore()
    useLocaleBinding(() => session.language)
  },
  template: '<StationPage />',
})

export async function mountPageFollowingTheChosenLanguage(): Promise<VueWrapper> {
  localStorage.setItem('language', 'de')
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.station = KITCHEN
  const page = mount(StationPageFollowingTheLanguage, {
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
  await flushPromises()
  return page
}

export function cardInTheQueue(page: VueWrapper, stationOrderId: string) {
  return page.get(
    `[data-test="orders-column"] [data-test="station-order"][data-test-id="${stationOrderId}"]`,
  )
}

export function itemOnCard(card: ReturnType<VueWrapper['get']>, orderItemId: string) {
  return card.get(`[data-test="station-item"][data-test-id="${orderItemId}"]`)
}

export function textsOf(selector: string): string[] {
  return [...document.querySelectorAll(selector)].map((element) => element.textContent?.trim() ?? '')
}
