import { expect, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { answer, stubLaptop, type LaptopReply, type StubbedLaptop } from './support/laptop'
import { aBrowserThatCountsTheTap, aTabThatOpenedTheAppAt } from './support/browserHistory'
import { clickOn, isOnScreen, textOnScreen, waitUntil } from './support/dom'
import { centreOf, drawingIn, shapeWithId, tapAt } from './support/konvaDrawing'
import { testPlugins } from './support/plugins'
import { aGeometry, aPlanArea, aPlanTable } from './support/wireViews'

const SESSION = {
  deviceId: 'device-1',
  staffMember: { id: 'staff-1', name: 'Anna' },
  station: null,
  language: 'de',
}

const WASSER = {
  id: 'item-wasser',
  name: 'Wasser',
  categoryId: 'category-getraenke',
  priceCents: 200,
  sortOrder: 1,
  isAvailable: true,
  stationIds: ['station-bar'],
  productionMinutes: null,
  isQueueIndependent: false,
}

const CATALOG = {
  festival: null,
  categories: [{ categoryId: 'category-getraenke', name: 'Getränke', colourHex: '#90CAF9', sortOrder: 1 }],
  items: [WASSER],
  stations: [{ id: 'station-bar', name: 'Bar', sortOrder: 1 }],
}

const HALLE = aPlanArea({
  areaId: 'area-halle',
  name: 'Halle',
  tables: [aPlanTable({ tableId: 'table-12', name: '12', geometry: aGeometry({ x: 20, y: 30, width: 80, height: 40 }) })],
})

const GARTEN = aPlanArea({ areaId: 'area-garten', name: 'Garten' })

export const AN_ACCEPTED_ORDER = {
  orderId: 'order-1',
  globalOrderNumber: 1,
  status: 'open',
  totalCents: 200,
  createdAtUtc: '2026-09-05T18:00:00Z',
  stationOrders: [],
}

export const THE_AREA_LIST = { areaList: true, title: '', sheet: '', orderScreen: false, summary: false, settleView: false }
export const THE_PLAN_OF_HALLE = { areaList: false, title: 'Halle', sheet: '', orderScreen: false, summary: false, settleView: false }
export const THE_SHEET_OF_TISCH_12 = { areaList: false, title: 'Halle', sheet: 'Tisch 12', orderScreen: false, summary: false, settleView: false }

export function aLaptopWithTwoAreas(orderReply: LaptopReply = answer(AN_ACCEPTED_ORDER)): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({}))
    .answers('GET', '/api/session', answer(SESSION))
    .answers('GET', '/api/catalog', answer(CATALOG))
    .answers('GET', '/api/table-plan', answer({ areas: [HALLE, GARTEN] }))
    .answers('POST', '/api/orders', orderReply)
}

export function aWaiterPhoneTabOpenedAtHome(): void {
  vi.resetModules()
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  aTabThatOpenedTheAppAt('/')
  aBrowserThatCountsTheTap()
  localStorage.setItem('deviceToken', 'lookup.token-here')
}

export async function aWaiterPhoneOnTheAreaList(): Promise<VueWrapper> {
  const { startRouter } = await import('../src/shared/router/router')
  startRouter()
  const App = (await import('../src/App.vue')).default
  const phone = mount(App, { global: { plugins: testPlugins(), stubs: { transition: false } }, attachTo: document.body })
  await waitUntil(() => expect(isOnScreen('[data-test="area-list"]')).toBe(true))
  return phone
}

export async function aWasserOnTheOrderForTisch12(): Promise<void> {
  const { useOrderStore } = await import('../src/phone/stores/order')
  const order = useOrderStore()
  order.chooseTable({ tableId: 'table-12', name: '12' })
  order.addItem({ catalogItemId: 'item-wasser', note: null, stationId: 'station-bar', name: 'Wasser' })
}

export async function openTheArea(areaId: string): Promise<void> {
  await clickOn(`[data-test="area-button"][data-test-id="${areaId}"]`)
  await waitUntil(() => expect(isOnScreen('[data-test="area-plan"]')).toBe(true))
}

export async function tapTheTable(tableId: string): Promise<void> {
  const drawing = drawingIn('[data-test="area-plan"]')
  await tapAt(drawing, centreOf(shapeWithId(drawing, `table-${tableId}`)))
  await waitUntil(() => expect(isOnScreen('[data-test="table-actions"]')).toBe(true))
}

export async function takeTheOrder(): Promise<void> {
  await clickOn('[data-test="take-order"]')
  await waitUntil(() => expect(isOnScreen('[data-test="catalog"]')).toBe(true))
}

export async function theSummaryOfTheOrderForTisch12InHalle(): Promise<void> {
  await aWaiterPhoneOnTheAreaList()
  await aWasserOnTheOrderForTisch12()
  await openTheArea('area-halle')
  await tapTheTable('table-12')
  await takeTheOrder()
  await clickOn('[data-test="to-review"]')
  await waitUntil(() => expect(isOnScreen('[data-test="send-and-settle-later"]')).toBe(true))
}

export function whatThePhoneShows() {
  return {
    areaList: isOnScreen('[data-test="area-list"]'),
    title: textOnScreen('[data-test="header-title"]').trim(),
    sheet: textOnScreen('[data-test="table-actions-title"]').trim(),
    orderScreen: isOnScreen('[data-test="catalog"]'),
    summary: isOnScreen('[data-test="review"]'),
    settleView: isOnScreen('[data-test="settle-title"]'),
  }
}
