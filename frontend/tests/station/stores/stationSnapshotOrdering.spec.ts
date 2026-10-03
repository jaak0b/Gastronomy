import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useStationStore } from '../../../src/station/stores/station'
import { aHold, answer, heldUntil, inTurn, stubLaptopAt } from '../../support/laptop'
import { aQueue, enrolledStationTablet, item, stationOrder } from '../../support/station'
import { aQueuedItem } from '../../support/wireViews'

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
    const readingAnswer = aHold()
    const fulfilAnswer = aHold()
    stubLaptopAt({
      '/api/station/orders': heldUntil(
        readingAnswer.released,
        aQueue([
          stationOrder(),
          stationOrder({
            stationOrderId: 'station-order-2',
            globalOrderNumber: 41,
            stationOrderNumber: 13,
            tableName: 'Tisch 7',
            items: [
              aQueuedItem({ orderItemId: 'c', itemName: 'Hotdog', note: 'Ohne Ketchup' }),
              aQueuedItem({ orderItemId: 'd', itemName: 'Hotdog', note: 'Ohne Ketchup' }),
            ],
          }),
        ]),
      ),
      '/api/station/items/fulfill': heldUntil(
        fulfilAnswer.released,
        aQueue([
          stationOrder({
            items: [item('a', 'Bratwurst', '2026-09-05T18:01:00Z'), item('b', 'Bratwurst')],
          }),
        ]),
      ),
    })
    const store = useStationStore()

    const fulfilling = store.fulfill(['a'])
    const refreshing = store.load()

    readingAnswer.release()
    await refreshing

    fulfilAnswer.release()
    await fulfilling

    expect(store.orders.map((shown) => shown.globalOrderNumber)).toContain(41)
  })

  it('does not put a handed out item back when an older reading answers last', async () => {
    enrolledStationTablet()
    const readingAnswer = aHold()
    const fulfilAnswer = aHold()
    stubLaptopAt({
      '/api/station/orders': heldUntil(readingAnswer.released, aQueue([stationOrder()])),
      '/api/station/items/fulfill': heldUntil(fulfilAnswer.released, aQueue([])),
    })
    const store = useStationStore()

    const refreshing = store.load()
    store.toggleItemSelection('a')
    store.toggleItemSelection('b')
    const fulfilling = store.fulfill(['a', 'b'])

    fulfilAnswer.release()
    await fulfilling

    readingAnswer.release()
    await refreshing

    expect(store.orders).toEqual([])
    expect(store.selectedItemIds).toEqual([])
  })

  it('keeps the working state while a newer action is still in flight', async () => {
    enrolledStationTablet()
    const fulfilAnswer = aHold()
    const putBackAnswer = aHold()
    stubLaptopAt({
      '/api/station/items/fulfill': heldUntil(fulfilAnswer.released, aQueue([])),
      '/api/station/items/unfulfill': heldUntil(putBackAnswer.released, aQueue([])),
    })
    const store = useStationStore()

    const fulfilling = store.fulfill(['a'])
    const puttingBack = store.unfulfill('b')

    fulfilAnswer.release()
    await fulfilling

    expect(store.isWorking).toBe(true)

    putBackAnswer.release()
    await puttingBack

    expect(store.isWorking).toBe(false)
  })

  it('keeps a stale failure from flagging a board a newer answer already loaded', async () => {
    enrolledStationTablet()
    const firstAnswer = aHold()
    const secondAnswer = aHold()
    stubLaptopAt({
      '/api/station/orders': inTurn(
        heldUntil(firstAnswer.released, answer({}, 500)),
        heldUntil(secondAnswer.released, aQueue([stationOrder()])),
      ),
    })
    const store = useStationStore()

    const firstLoad = store.load()
    const secondLoad = store.load()

    secondAnswer.release()
    await secondLoad

    firstAnswer.release()
    await firstLoad

    expect(store.loadFailed).toBe(false)
    expect(store.loadFailureKey).toBeNull()
  })
})
