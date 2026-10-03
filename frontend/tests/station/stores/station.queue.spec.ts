import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useStationStore } from '../../../src/station/stores/station'
import { stubLaptopAt, refusal, inTurn } from '../../support/laptop'
import { KITCHEN, item, stationOrder, aQueue, enrolledStationTablet } from './stationFixture'

describe('the orders a station tablet is showing', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('names the station the laptop says this tablet belongs to', async () => {
    stubLaptopAt({ '/api/station/orders': aQueue([stationOrder()]) })
    enrolledStationTablet()
    const station = useStationStore()

    await station.load()

    expect(station.identity).toEqual(KITCHEN)
  })

  it('keeps the orders in the order the laptop sent them', async () => {
    const first = stationOrder({ stationOrderId: 'station-order-1', stationOrderNumber: 12 })
    const second = stationOrder({ stationOrderId: 'station-order-2', stationOrderNumber: 14 })
    stubLaptopAt({ '/api/station/orders': aQueue([first, second]) })
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
    stubLaptopAt({ '/api/station/orders': aQueue([visible, hidden, together], [visible]) })
    enrolledStationTablet()
    const station = useStationStore()

    await station.load()

    expect(station.asItComes.map((entry) => entry.stationOrderId)).toEqual(['station-order-1'])
  })

  it('says the list may be out of date when the laptop could not be reached', async () => {
    stubLaptopAt({
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
    stubLaptopAt({ '/api/station/orders': aQueue([stationOrder()]) })
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
    stubLaptopAt({
      '/api/station/orders': inTurn(aQueue([firstAnswer]), aQueue([secondAnswer])),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()
    station.toggleItemSelection('a')
    station.toggleItemSelection('b')

    await station.load()

    expect(station.selectedItemIds).toEqual(['b'])
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
    const laptop = stubLaptopAt({
      '/api/station/orders': aQueue([visible]),
      '/api/station/orders/station-order-1/hide': aQueue([hidden]),
    })
    enrolledStationTablet()
    const station = useStationStore()
    await station.load()

    await station.hide('station-order-1')

    expect(laptop.writtenBodies()).toEqual([undefined])
    expect(station.orders.map((entry) => entry.stationOrderId)).toEqual(['station-order-1'])
    expect(station.asItComes).toEqual([])
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
    stubLaptopAt({
      '/api/station/orders': refusal('errors.station.noFestivalIsRunning', { code: 'NoRunningFestival' }),
    })
    const station = useStationStore()

    await station.load()

    expect(station.loadFailed).toBe(true)
    expect(station.loadFailureKey).toBe('errors.station.noFestivalIsRunning')
  })

  it('is unnamed when the laptop could not be reached at all', async () => {
    stubLaptopAt({
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
    stubLaptopAt({
      '/api/station/orders': inTurn(
        refusal('errors.station.notPartOfTheFestival', { code: 'StationNotAtTheFestival' }),
        aQueue([stationOrder()]),
      ),
    })
    const station = useStationStore()
    await station.load()

    await station.load()

    expect(station.loadFailed).toBe(false)
    expect(station.loadFailureKey).toBeNull()
  })
})
