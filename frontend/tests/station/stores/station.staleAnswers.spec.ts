import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useStationStore } from '../../../src/station/stores/station'
import { aHold, answer, heldUntil, inTurn, stubLaptopAt } from '../../support/laptop'
import { aQueue, enrolledStationTablet, item, stationOrder } from '../../support/station'

const HANDED_OUT = '2026-09-05T18:30:00Z'

describe('a station tablet whose older reading answers after a newer one', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    enrolledStationTablet()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps the newer done list instead of the one the slow reading brings', async () => {
    const slowReading = aHold()
    const olderList = stationOrder({ stationOrderId: 'station-order-old', fulfilledItemCount: 1 })
    const newerList = stationOrder({ stationOrderId: 'station-order-new', fulfilledItemCount: 1 })
    stubLaptopAt({
      '/api/station/orders/fulfilled': inTurn(
        heldUntil(slowReading.released, answer({ stationOrders: [olderList] })),
        answer({ stationOrders: [newerList] }),
      ),
    })
    const station = useStationStore()
    const first = station.loadFulfilled()
    await station.loadFulfilled()

    slowReading.release()
    await first

    expect(station.fulfilled.map((order) => order.stationOrderId)).toEqual(['station-order-new'])
  })

  it('keeps an item that was put back open when a reading from before answers last', async () => {
    const slowReading = aHold()
    const openAgain = stationOrder({ items: [item('a', 'Bratwurst'), item('b', 'Pommes')] })
    stubLaptopAt({
      '/api/station/orders': heldUntil(slowReading.released, aQueue([])),
      '/api/station/items/unfulfill': aQueue([openAgain]),
    })
    const station = useStationStore()
    const reading = station.load()
    await station.unfulfill('b')

    slowReading.release()
    await reading

    expect(station.orders.map((order) => order.stationOrderId)).toEqual(['station-order-1'])
  })

  it('keeps a hidden order out of the second column when a reading from before answers last', async () => {
    const slowReading = aHold()
    const asItComes = stationOrder({
      deliveryMode: 'asItComes',
      items: [item('a', 'Bier'), item('b', 'Wasser', HANDED_OUT)],
    })
    stubLaptopAt({
      '/api/station/orders': heldUntil(slowReading.released, aQueue([asItComes], [asItComes])),
    }).answers('POST', /\/hide$/, aQueue([asItComes], []))
    const station = useStationStore()
    const reading = station.load()
    await station.hide('station-order-1')

    slowReading.release()
    await reading

    expect(station.asItComes).toEqual([])
  })
})
