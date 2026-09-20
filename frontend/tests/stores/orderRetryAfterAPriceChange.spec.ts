import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { CatalogItem } from '../../src/core/apiTypes'
import { useCatalogStore } from '../../src/stores/catalog'
import { useOrderStore } from '../../src/stores/order'

function menuItem(id: string, name: string, priceCents: number): CatalogItem {
  return {
    id,
    name,
    categoryId: 'category-1',
    priceCents,
    sortOrder: 1,
    isAvailable: true,
    stationIds: ['station-bar'],
    productionMinutes: null,
    isQueueIndependent: false,
  }
}

function menuWith(items: CatalogItem[]) {
  useCatalogStore().catalog = {
    festival: { festivalId: 'festival-1', name: 'Sommerfest' },
    categories: [{ categoryId: 'category-1', name: 'Speisen und Getraenke', sortOrder: 1 }],
    items,
    stations: [{ id: 'station-bar', name: 'Theke', sortOrder: 1, isActive: true }],
  }
}

function theLaptopNeverAnswers() {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => {
      throw new TypeError('the laptop cannot be reached')
    }),
  )
}

function theLaptopTakesTheOrder() {
  vi.stubGlobal(
    'fetch',
    vi.fn(
      async () =>
        new Response(
          JSON.stringify({
            orderId: 'order-1',
            globalOrderNumber: 1,
            totalCents: 650,
            createdAtUtc: '2026-09-05T18:00:00Z',
            stationOrders: [],
          }),
          { status: 200 },
        ),
    ),
  )
}

function bodyOfTheLastCall() {
  const calls = vi.mocked(fetch).mock.calls
  return JSON.parse((calls[calls.length - 1][1] as RequestInit).body as string)
}

describe('sending an already settled order again after the menu price changed', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function anOrderSettledAtSixFifty() {
    menuWith([menuItem('item-brat', 'Bratwurst', 350), menuItem('item-bier', 'Bier', 300)])
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-brat',
      note: null,
      stationId: 'station-bar',
      name: 'Bratwurst',
    })
    order.addItem({
      catalogItemId: 'item-bier',
      note: null,
      stationId: 'station-bar',
      name: 'Bier',
    })
    order.setTable('Tisch 5')
    theLaptopNeverAnswers()
    await order.send({ amountPaidCents: 650, paymentNotice: null })
    return order
  }

  it('sends the prices the waiter collected the money against', async () => {
    const order = await anOrderSettledAtSixFifty()
    menuWith([menuItem('item-brat', 'Bratwurst', 350), menuItem('item-bier', 'Bier', 350)])
    theLaptopTakesTheOrder()

    await order.sendAgain()

    const sent = bodyOfTheLastCall()
    expect(sent.items.map((item: { unitPriceCents: number }) => item.unitPriceCents)).toEqual([
      350, 300,
    ])
  })

  it('keeps the settlement and the prices in the same order, so nothing is paid twice or short', async () => {
    const order = await anOrderSettledAtSixFifty()
    menuWith([menuItem('item-brat', 'Bratwurst', 350), menuItem('item-bier', 'Bier', 250)])
    theLaptopTakesTheOrder()

    await order.sendAgain()

    const sent = bodyOfTheLastCall()
    expect({
      paidPrices: sent.items.map(
        (item: { settlement: { paidPriceCents: number } }) => item.settlement.paidPriceCents,
      ),
      prices: sent.items.map((item: { unitPriceCents: number }) => item.unitPriceCents),
    }).toEqual({ paidPrices: [350, 300], prices: [350, 300] })
  })
})
