import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { Catalog } from '../../../src/shared/api/apiTypes'
import { saveDraft } from '../../../src/phone/core/draftCart'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'

const BRATWURST_ID = 'bratwurst'
const BIER_ID = 'bier'
const KUECHE_ID = 'kueche'
const THEKE_ID = 'theke'

function menuWithBratwurst(): Catalog {
  return {
    festival: { festivalId: 'fest-1', name: 'Sommerfest' },
    categories: [
      { categoryId: 'speisen', name: 'Speisen', colourHex: '#aa0000', sortOrder: 1 },
      { categoryId: 'getraenke', name: 'Getränke', colourHex: '#0000aa', sortOrder: 2 },
    ],
    items: [
      {
        id: BRATWURST_ID,
        name: 'Bratwurst',
        categoryId: 'speisen',
        priceCents: 250,
        sortOrder: 1,
        isAvailable: true,
        stationIds: [KUECHE_ID],
        productionMinutes: 5,
        isQueueIndependent: false,
      },
      {
        id: BIER_ID,
        name: 'Bier',
        categoryId: 'getraenke',
        priceCents: 300,
        sortOrder: 1,
        isAvailable: true,
        stationIds: [THEKE_ID],
        productionMinutes: null,
        isQueueIndependent: false,
      },
    ],
    stations: [
      { id: KUECHE_ID, name: 'Küche', sortOrder: 1 },
      { id: THEKE_ID, name: 'Theke', sortOrder: 2 },
    ],
  }
}

function menuAfterSpeisenWasSwitchedOff(): Catalog {
  const menu = menuWithBratwurst()
  return {
    ...menu,
    categories: menu.categories.filter((category) => category.categoryId === 'getraenke'),
    items: menu.items.filter((item) => item.id === BIER_ID),
  }
}

function draftForZeltVier(): void {
  saveDraft({
    festivalId: 'fest-1',
    tableName: 'Zelt 4',
    note: null,
    clientOrderId: '11111111-1111-4111-8111-111111111111',
    deliveryModes: {},
    lines: [
      { catalogItemId: BRATWURST_ID, note: null, stationId: KUECHE_ID, name: 'Bratwurst', stationName: 'Küche' },
      { catalogItemId: BRATWURST_ID, note: null, stationId: KUECHE_ID, name: 'Bratwurst', stationName: 'Küche' },
      { catalogItemId: BRATWURST_ID, note: null, stationId: KUECHE_ID, name: 'Bratwurst', stationName: 'Küche' },
      { catalogItemId: BIER_ID, note: null, stationId: THEKE_ID, name: 'Bier', stationName: 'Theke' },
      { catalogItemId: BIER_ID, note: null, stationId: THEKE_ID, name: 'Bier', stationName: 'Theke' },
    ],
  })
}

interface SubmittedItem {
  catalogItemId: string
  unitPriceCents: number
}

describe('a retry after the article left the menu', () => {
  let submittedItems: SubmittedItem[][]

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    submittedItems = []
    let attempt = 0
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_input: unknown, init?: RequestInit) => {
        attempt += 1
        const body = JSON.parse(String(init?.body)) as { items: SubmittedItem[] }
        submittedItems.push(body.items)
        if (attempt === 1) {
          throw new TypeError('Failed to fetch')
        }
        return new Response(
          JSON.stringify({
            orderId: 'order-1',
            globalOrderNumber: 7,
            totalCents: 0,
            createdAtUtc: '2026-09-05T18:00:00Z',
            stationOrders: [],
          }),
          { status: 200 },
        )
      }),
    )
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('never sends a Bratwurst at nothing when the waiter taps send again', async () => {
    draftForZeltVier()
    const catalog = useCatalogStore()
    catalog.catalog = menuWithBratwurst()
    const order = useOrderStore()

    await order.send(null)
    expect(order.sendHasFailed).toBe(true)

    catalog.catalog = menuAfterSpeisenWasSwitchedOff()

    await order.sendAgain()

    expect(submittedItems).toHaveLength(2)
    expect((submittedItems[1] ?? []).map((item) => item.unitPriceCents)).toEqual([
      250, 250, 250, 300, 300,
    ])
  })
})
