import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { StationOrder, StationOrderItem } from '../../../src/shared/api/apiTypes'
import { useStationStore } from '../../../src/station/stores/station'
import { useSessionStore } from '../../../src/shared/stores/session'

const KITCHEN = { id: 'station-kueche', name: 'Küche' }

function item(
  orderItemId: string,
  itemName: string,
  fulfilledAtUtc: string | null = null,
): StationOrderItem {
  return { orderItemId, itemName, note: null, fulfilledAtUtc }
}

function stationOrder(overrides: Partial<StationOrder> = {}): StationOrder {
  return {
    stationOrderId: 'station-order-1',
    globalOrderNumber: 137,
    stationOrderNumber: 12,
    tableName: 'Tisch 3',
    deliveryMode: 'together',
    createdAtUtc: '2026-09-05T18:00:00Z',
    isHiddenFromAsItComesQueue: false,
    itemCount: 2,
    fulfilledItemCount: 0,
    items: [item('a', 'Bratwurst'), item('b', 'Pommes')],
    ...overrides,
  }
}

function ok(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200 })
}

function refused(code: string, messageKey: string): Response {
  return new Response(JSON.stringify({ code, messageKey, parameters: {}, details: null }), {
    status: 409,
  })
}

function aQueue(orders: StationOrder[], asItComes: StationOrder[] = []): Response {
  return ok({ station: KITCHEN, orders, asItComes })
}

function enrolledStationTablet(): void {
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.deviceKind = 'station'
}

function stubTheLaptop(routes: Record<string, () => Response>): {
  posts: unknown[]
  urls: string[]
} {
  const posts: unknown[] = []
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options?: RequestInit) => {
      urls.push(url)
      if (options?.method === 'POST') {
        posts.push(JSON.parse(String(options?.body ?? 'null')))
      }
      const answer = routes[url]
      if (answer === undefined) {
        throw new TypeError(`the stub has no answer for ${url}`)
      }
      return answer()
    }),
  )
  return { posts, urls }
}

describe('the orders a station tablet is showing', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('names the station the laptop says this tablet belongs to', async () => {
    stubTheLaptop({ '/api/station/orders': () => aQueue([stationOrder()]) })
    enrolledStationTablet()
    const station = useStationStore()

    await station.load()

    expect(station.identity).toEqual(KITCHEN)
  })

  it('keeps the orders in the order the laptop sent them', async () => {
    const first = stationOrder({ stationOrderId: 'station-order-1', stationOrderNumber: 12 })
    const second = stationOrder({ stationOrderId: 'station-order-2', stationOrderNumber: 14 })
    stubTheLaptop({ '/api/station/orders': () => aQueue([first, second]) })
    enrolledStationTablet()
    const station = useStationStore()

    await station.load()

    expect(station.orders.map((entry) => entry.stationOrderNumber)).toEqual([12, 14])
  })

  it('puts only the visible as-it-comes orders in the second column', async () => {
    const visible = stationOrder({
      stationOrderId: 'station-order-1',
      deliveryMode: 'asItComes',
      stationOrderNumber: 11,
    })
    const hidden = stationOrder({
      stationOrderId: 'station-order-2',
      deliveryMode: 'asItComes',
      stationOrderNumber: 12,
      isHiddenFromAsItComesQueue: true,
    })
    const together = stationOrder({ stationOrderId: 'station-order-3', stationOrderNumber: 13 })
    stubTheLaptop({ '/api/station/orders': () => aQueue([visible, hidden, together], [visible]) })
    enrolledStationTablet()
    const station = useStationStore()

    await station.load()

    expect(station.asItComes.map((entry) => entry.stationOrderId)).toEqual(['station-order-1'])
  })

  it('says the list may be out of date when the laptop could not be reached', async () => {
    stubTheLaptop({
      '/api/station/orders': () => {
        throw new TypeError('Failed to fetch')
      },
    })
    enrolledStationTablet()
    const station = useStationStore()

    await station.load()

    expect(station.loadFailed).toBe(true)
  })
})

describe('selecting items for the done control', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('adds an item on the first tap and takes it off again on the second', async () => {
    stubTheLaptop({ '/api/station/orders': () => aQueue([stationOrder()]) })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    station.toggleItemSelection('a')
    expect(station.selectedItemIds).toEqual(['a'])

    station.toggleItemSelection('a')
    expect(station.selectedItemIds).toEqual([])
  })

  it('drops the ids whose item is no longer open after a reload', async () => {
    const firstAnswer = stationOrder({ items: [item('a', 'Bratwurst'), item('b', 'Pommes')] })
    const secondAnswer = stationOrder({ itemCount: 1, items: [item('b', 'Pommes')] })
    let queue = firstAnswer
    stubTheLaptop({ '/api/station/orders': () => aQueue([queue]) })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()
    station.toggleItemSelection('a')
    station.toggleItemSelection('b')

    queue = secondAnswer
    await station.load()

    expect(station.selectedItemIds).toEqual(['b'])
  })
})

describe('marking selected items as done', () => {
  const HALF_DONE = stationOrder({
    itemCount: 2,
    fulfilledItemCount: 1,
    items: [item('a', 'Bratwurst', '2026-09-05T18:30:00Z'), item('b', 'Pommes')],
  })

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('tells the laptop which items are done', async () => {
    const { posts } = stubTheLaptop({
      '/api/station/orders': () => aQueue([stationOrder()]),
      '/api/station/items/fulfill': () => aQueue([HALF_DONE]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.fulfill(['a'])

    expect(posts).toEqual([{ orderItemIds: ['a'] }])
  })

  it('takes the fresh list out of the answer, so the screen matches the laptop', async () => {
    stubTheLaptop({
      '/api/station/orders': () => aQueue([stationOrder()]),
      '/api/station/items/fulfill': () => aQueue([HALF_DONE]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.fulfill(['a'])

    expect(station.orders[0].fulfilledItemCount).toBe(1)
    expect(station.orders[0].items[0].fulfilledAtUtc).toBe('2026-09-05T18:30:00Z')
  })

  it('takes the done items out of the selection and leaves the other ones', async () => {
    stubTheLaptop({
      '/api/station/orders': () => aQueue([stationOrder()]),
      '/api/station/items/fulfill': () => aQueue([HALF_DONE]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()
    station.toggleItemSelection('a')
    station.toggleItemSelection('b')

    await station.fulfill(['a'])

    expect(station.selectedItemIds).toEqual(['b'])
  })

  it('takes an order off the board once every one of its items is done', async () => {
    stubTheLaptop({
      '/api/station/orders': () => aQueue([stationOrder()]),
      '/api/station/items/fulfill': () => aQueue([]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()
    station.toggleItemSelection('a')

    await station.fulfill(['a'])

    expect(station.orders).toEqual([])
    expect(station.selectedItemIds).toEqual([])
  })

  it('shows the reason the laptop gave and leaves the list and the selection as they were', async () => {
    stubTheLaptop({
      '/api/station/orders': () => aQueue([stationOrder()]),
      '/api/station/items/fulfill': () => refused('ItemNotFulfilled', 'station.changeNotSaved'),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()
    station.toggleItemSelection('a')

    await station.fulfill(['a'])

    expect(station.failureKey).toBe('station.changeNotSaved')
    expect(station.selectedItemIds).toEqual(['a'])
    expect(station.orders[0].fulfilledItemCount).toBe(0)
  })

  it('asks the person to tap again when the laptop could not be reached', async () => {
    stubTheLaptop({
      '/api/station/orders': () => aQueue([stationOrder()]),
      '/api/station/items/fulfill': () => {
        throw new TypeError('Failed to fetch')
      },
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.fulfill(['a'])

    expect(station.failureKey).toBe('station.actionNotReached')
  })
})

describe('putting one item back from the done view', () => {
  const DONE_STATION_ORDER = stationOrder({
    itemCount: 2,
    fulfilledItemCount: 1,
    items: [item('a', 'Bratwurst'), item('b', 'Pommes', '2026-09-05T18:30:00Z')],
  })

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('tells the laptop which item is open again', async () => {
    const { posts } = stubTheLaptop({
      '/api/station/orders': () => aQueue([]),
      '/api/station/orders/fulfilled': () => ok({ stationOrders: [DONE_STATION_ORDER] }),
      '/api/station/items/unfulfill': () => aQueue([stationOrder()]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.loadFulfilled()

    await station.unfulfill('b')

    expect(posts).toEqual([{ orderItemIds: ['b'] }])
  })

  it('brings the order back into the queue and out of the done list', async () => {
    const openAgain = stationOrder({
      itemCount: 2,
      fulfilledItemCount: 0,
      items: [item('a', 'Bratwurst'), item('b', 'Pommes')],
    })
    let doneStationOrders = [DONE_STATION_ORDER]
    stubTheLaptop({
      '/api/station/orders': () => aQueue([]),
      '/api/station/orders/fulfilled': () => ok({ stationOrders: doneStationOrders }),
      '/api/station/items/unfulfill': () => aQueue([openAgain]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()
    await station.openFulfilled()
    doneStationOrders = []

    await station.unfulfill('b')

    expect(station.orders.map((entry) => entry.stationOrderId)).toEqual(['station-order-1'])
    expect(station.fulfilled).toEqual([])
  })
})

describe('hiding an order from the second column', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks the laptop to hide it and leaves it in the first column only', async () => {
    const visible = stationOrder({
      stationOrderId: 'station-order-1',
      deliveryMode: 'asItComes',
      stationOrderNumber: 11,
    })
    const hidden = { ...visible, isHiddenFromAsItComesQueue: true }
    const { posts } = stubTheLaptop({
      '/api/station/orders': () => aQueue([visible]),
      '/api/station/orders/station-order-1/hide': () => aQueue([hidden]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.hide('station-order-1')

    expect(posts).toEqual([null])
    expect(station.orders.map((entry) => entry.stationOrderId)).toEqual(['station-order-1'])
    expect(station.asItComes).toEqual([])
  })
})

describe('the done view', () => {
  const DONE_STATION_ORDER = stationOrder({
    itemCount: 2,
    fulfilledItemCount: 2,
    items: [
      item('a', 'Bratwurst', '2026-09-05T18:30:00Z'),
      item('b', 'Pommes', '2026-09-05T18:31:00Z'),
    ],
  })

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('loads the orders with at least one done item when it is opened', async () => {
    const { urls } = stubTheLaptop({
      '/api/station/orders': () => aQueue([]),
      '/api/station/orders/fulfilled': () => ok({ stationOrders: [DONE_STATION_ORDER] }),
    })
    enrolledStationTablet()
    const station = useStationStore()

    await station.openFulfilled()

    expect(station.isShowingFulfilled).toBe(true)
    expect(station.fulfilled.map((entry) => entry.stationOrderId)).toEqual(['station-order-1'])
    expect(urls).toEqual(['/api/station/orders/fulfilled'])
  })

  it('goes back to the queue when the employee asks for it', async () => {
    stubTheLaptop({
      '/api/station/orders': () => aQueue([]),
      '/api/station/orders/fulfilled': () => ok({ stationOrders: [DONE_STATION_ORDER] }),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.openFulfilled()

    station.closeFulfilled()

    expect(station.isShowingFulfilled).toBe(false)
  })

  it('says so when nothing is done yet and stays quiet while the answer is missing', async () => {
    stubTheLaptop({
      '/api/station/orders': () => aQueue([]),
      '/api/station/orders/fulfilled': () => ok({ stationOrders: [] }),
    })
    enrolledStationTablet()
    const station = useStationStore()

    expect(station.hasNothingDone).toBe(false)

    await station.loadFulfilled()

    expect(station.hasNothingDone).toBe(true)
  })

  it('says the list may be out of date when the laptop could not be reached', async () => {
    stubTheLaptop({
      '/api/station/orders': () => aQueue([]),
      '/api/station/orders/fulfilled': () => {
        throw new TypeError('Failed to fetch')
      },
    })
    enrolledStationTablet()
    const station = useStationStore()

    await station.loadFulfilled()

    expect(station.fulfilledLoadFailed).toBe(true)
    expect(station.hasNothingDone).toBe(false)
  })
})

describe('why a station tablet could not load its orders', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    enrolledStationTablet()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is the reason the laptop named, so the tablet can say no festival is running', async () => {
    stubTheLaptop({
      '/api/station/orders': () => refused('NoRunningFestival', 'station.noFestivalIsRunning'),
    })
    const station = useStationStore()

    await station.load()

    expect(station.loadFailed).toBe(true)
    expect(station.loadFailureKey).toBe('station.noFestivalIsRunning')
  })

  it('is unnamed when the laptop could not be reached at all', async () => {
    stubTheLaptop({
      '/api/station/orders': () => {
        throw new TypeError('the laptop is not there')
      },
    })
    const station = useStationStore()

    await station.load()

    expect(station.loadFailed).toBe(true)
    expect(station.loadFailureKey).toBeNull()
  })

  it('is forgotten once the orders come through again', async () => {
    let refusal = true
    stubTheLaptop({
      '/api/station/orders': () =>
        refusal
          ? refused('StationNotAtTheFestival', 'station.notPartOfTheFestival')
          : aQueue([stationOrder()]),
    })
    const station = useStationStore()
    await station.load()

    refusal = false
    await station.load()

    expect(station.loadFailed).toBe(false)
    expect(station.loadFailureKey).toBeNull()
  })
})
