import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useStationStore } from '../../../src/station/stores/station'
import { stubLaptopAt, answer, refusal, inTurn } from '../../support/laptop'
import { item, stationOrder, aQueue, enrolledStationTablet } from './stationFixture'

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
    const laptop = stubLaptopAt({
      '/api/station/orders': aQueue([stationOrder()]),
      '/api/station/items/fulfill': aQueue([HALF_DONE]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.fulfill(['a'])

    expect(laptop.writtenBodies()).toEqual([{ orderItemIds: ['a'] }])
  })

  it('takes the fresh list out of the answer, so the screen matches the laptop', async () => {
    stubLaptopAt({
      '/api/station/orders': aQueue([stationOrder()]),
      '/api/station/items/fulfill': aQueue([HALF_DONE]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.fulfill(['a'])

    expect(station.orders[0].fulfilledItemCount).toBe(1)
    expect(station.orders[0].items[0].fulfilledAtUtc).toBe('2026-09-05T18:30:00Z')
  })

  it('takes the done items out of the selection and leaves the other ones', async () => {
    stubLaptopAt({
      '/api/station/orders': aQueue([stationOrder()]),
      '/api/station/items/fulfill': aQueue([HALF_DONE]),
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
    stubLaptopAt({
      '/api/station/orders': aQueue([stationOrder()]),
      '/api/station/items/fulfill': aQueue([]),
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
    stubLaptopAt({
      '/api/station/orders': aQueue([stationOrder()]),
      '/api/station/items/fulfill': refusal('errors.station.changeNotSaved', { code: 'ItemNotFulfilled' }),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()
    station.toggleItemSelection('a')

    await station.fulfill(['a'])

    expect(station.failureKey).toBe('errors.station.changeNotSaved')
    expect(station.selectedItemIds).toEqual(['a'])
    expect(station.orders[0].fulfilledItemCount).toBe(0)
  })

  it('asks the person to tap again when the laptop could not be reached', async () => {
    stubLaptopAt({
      '/api/station/orders': aQueue([stationOrder()]),
      '/api/station/items/fulfill': () => {
        throw new TypeError('Failed to fetch')
      },
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.fulfill(['a'])

    expect(station.failureKey).toBe('station.board.errors.actionNotReached')
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
    const laptop = stubLaptopAt({
      '/api/station/orders': aQueue([]),
      '/api/station/orders/fulfilled': answer({ stationOrders: [DONE_STATION_ORDER] }),
      '/api/station/items/unfulfill': aQueue([stationOrder()]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.loadFulfilled()

    await station.unfulfill('b')

    expect(laptop.writtenBodies()).toEqual([{ orderItemIds: ['b'] }])
  })

  it('brings the order back into the queue and out of the done list', async () => {
    const openAgain = stationOrder({
      itemCount: 2,
      fulfilledItemCount: 0,
      items: [item('a', 'Bratwurst'), item('b', 'Pommes')],
    })
    stubLaptopAt({
      '/api/station/orders': aQueue([]),
      '/api/station/orders/fulfilled': inTurn(
        answer({ stationOrders: [DONE_STATION_ORDER] }),
        answer({ stationOrders: [] }),
      ),
      '/api/station/items/unfulfill': aQueue([openAgain]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()
    await station.openFulfilled()

    await station.unfulfill('b')

    expect(station.orders.map((entry) => entry.stationOrderId)).toEqual(['station-order-1'])
    expect(station.fulfilled).toEqual([])
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
    const laptop = stubLaptopAt({
      '/api/station/orders': aQueue([]),
      '/api/station/orders/fulfilled': answer({ stationOrders: [DONE_STATION_ORDER] }),
    })
    enrolledStationTablet()
    const station = useStationStore()

    await station.openFulfilled()

    expect(station.isShowingFulfilled).toBe(true)
    expect(station.fulfilled.map((entry) => entry.stationOrderId)).toEqual(['station-order-1'])
    expect(laptop.urls()).toEqual(['/api/station/orders/fulfilled'])
  })

  it('goes back to the queue when the employee asks for it', async () => {
    stubLaptopAt({
      '/api/station/orders': aQueue([]),
      '/api/station/orders/fulfilled': answer({ stationOrders: [DONE_STATION_ORDER] }),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.openFulfilled()

    station.closeFulfilled()

    expect(station.isShowingFulfilled).toBe(false)
  })

  it('says so when nothing is done yet and stays quiet while the answer is missing', async () => {
    stubLaptopAt({
      '/api/station/orders': aQueue([]),
      '/api/station/orders/fulfilled': answer({ stationOrders: [] }),
    })
    enrolledStationTablet()
    const station = useStationStore()

    expect(station.hasNothingDone).toBe(false)

    await station.loadFulfilled()

    expect(station.hasNothingDone).toBe(true)
  })

  it('says the list may be out of date when the laptop could not be reached', async () => {
    stubLaptopAt({
      '/api/station/orders': aQueue([]),
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
