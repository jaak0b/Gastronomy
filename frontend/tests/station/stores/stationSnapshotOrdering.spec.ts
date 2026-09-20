import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { StationOrder, StationOrderItem } from '../../../src/shared/api/apiTypes'
import { useStationStore } from '../../../src/station/stores/station'
import { useSessionStore } from '../../../src/shared/stores/session'

const KITCHEN = { id: 'station-kueche', name: 'Küche' }

function item(
  orderItemId: string,
  itemName: string,
  note: string | null = null,
  fulfilledAtUtc: string | null = null,
): StationOrderItem {
  return { orderItemId, itemName, note, fulfilledAtUtc }
}

function stationOrder(overrides: Partial<StationOrder> = {}): StationOrder {
  return {
    stationOrderId: 'station-order-1',
    globalOrderNumber: 40,
    stationOrderNumber: 12,
    tableName: 'Tisch 3',
    deliveryMode: 'together',
    createdAtUtc: '2026-09-05T18:00:00Z',
    isHiddenFromAsItComesQueue: false,
    itemCount: 2,
    fulfilledItemCount: 0,
    items: [item('a', 'Bratwurst'), item('b', 'Bratwurst')],
    ...overrides,
  }
}

function queueBody(orders: StationOrder[]): unknown {
  return { station: KITCHEN, orders, asItComes: [] }
}

function stubTheLaptopWithAnswersHeldBack(): {
  answer: (url: string, body: unknown, status?: number) => void
  answerTheNewest: (url: string, body: unknown, status?: number) => void
} {
  const waiting = new Map<string, ((response: Response) => void)[]>()

  function pendingFor(url: string): ((response: Response) => void)[] {
    const pending = waiting.get(url) ?? []
    waiting.set(url, pending)
    return pending
  }

  function releaseTheOldestOrNewest(
    url: string,
    body: unknown,
    status: number,
    newest: boolean,
  ): void {
    const pending = waiting.get(url)
    const resolve = newest ? pending?.pop() : pending?.shift()
    if (resolve === undefined) {
      throw new Error(`nothing is waiting on ${url}`)
    }
    if (pending !== undefined && pending.length === 0) {
      waiting.delete(url)
    }
    resolve(new Response(JSON.stringify(body), { status }))
  }

  vi.stubGlobal(
    'fetch',
    vi.fn(
      (url: string) =>
        new Promise<Response>((resolve) => {
          pendingFor(url).push(resolve)
        }),
    ),
  )
  return {
    answer: (url, body, status = 200) => releaseTheOldestOrNewest(url, body, status, false),
    answerTheNewest: (url, body, status = 200) => releaseTheOldestOrNewest(url, body, status, true),
  }
}

function enrolledStationTablet(): void {
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.deviceKind = 'station'
}

describe('a station tablet whose answers come back out of order', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps the order that arrived while a slow fulfilment was still in flight', async () => {
    enrolledStationTablet()
    const laptop = stubTheLaptopWithAnswersHeldBack()
    const store = useStationStore()

    const fulfilling = store.fulfill(['a'])
    const refreshing = store.load()

    laptop.answer(
      '/api/station/orders',
      queueBody([
        stationOrder(),
        stationOrder({
          stationOrderId: 'station-order-2',
          globalOrderNumber: 41,
          stationOrderNumber: 13,
          tableName: 'Tisch 7',
          items: [item('c', 'Hotdog', 'Ohne Ketchup'), item('d', 'Hotdog', 'Ohne Ketchup')],
        }),
      ]),
    )
    await refreshing

    laptop.answer(
      '/api/station/items/fulfill',
      queueBody([
        stationOrder({
          items: [item('a', 'Bratwurst', null, '2026-09-05T18:01:00Z'), item('b', 'Bratwurst')],
        }),
      ]),
    )
    await fulfilling

    expect(store.orders.map((shown) => shown.globalOrderNumber)).toContain(41)
  })

  it('does not put a handed out item back when an older reading answers last', async () => {
    enrolledStationTablet()
    const laptop = stubTheLaptopWithAnswersHeldBack()
    const store = useStationStore()

    const refreshing = store.load()
    store.toggleItemSelection('a')
    store.toggleItemSelection('b')
    const fulfilling = store.fulfill(['a', 'b'])

    laptop.answer('/api/station/items/fulfill', queueBody([]))
    await fulfilling

    laptop.answer('/api/station/orders', queueBody([stationOrder()]))
    await refreshing

    expect(store.orders).toEqual([])
    expect(store.selectedItemIds).toEqual([])
  })

  it('keeps the working state while a newer action is still in flight', async () => {
    enrolledStationTablet()
    const laptop = stubTheLaptopWithAnswersHeldBack()
    const store = useStationStore()

    const fulfilling = store.fulfill(['a'])
    const puttingBack = store.unfulfill('b')

    laptop.answer('/api/station/items/fulfill', queueBody([]))
    await fulfilling

    expect(store.isWorking).toBe(true)

    laptop.answer('/api/station/items/unfulfill', queueBody([]))
    await puttingBack

    expect(store.isWorking).toBe(false)
  })

  it('keeps a stale failure from flagging a board a newer answer already loaded', async () => {
    enrolledStationTablet()
    const laptop = stubTheLaptopWithAnswersHeldBack()
    const store = useStationStore()

    const firstLoad = store.load()
    const secondLoad = store.load()

    laptop.answerTheNewest('/api/station/orders', queueBody([stationOrder()]))
    await secondLoad

    laptop.answer('/api/station/orders', {}, 500)
    await firstLoad

    expect(store.loadFailed).toBe(false)
    expect(store.loadFailureKey).toBeNull()
  })
})
