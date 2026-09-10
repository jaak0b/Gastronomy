import { describe, expect, it } from 'vitest'
import { stationDeliveries } from '../../src/core/stationDeliveries'
import type { BasketLineView } from '../../src/core/basket'
import type { DeliveryMode, StationEstimate } from '../../src/core/apiTypes'

const QUEUES: StationEstimate[] = [
  { stationId: 'station-kueche', queuedMinutes: 12 },
  { stationId: 'station-theke', queuedMinutes: 0 },
]

function line(overrides: Partial<BasketLineView> = {}): BasketLineView {
  return {
    catalogItemId: 'item-bratwurst',
    name: 'Bratwurst',
    unitPriceCents: 350,
    note: null,
    stationId: 'station-kueche',
    stationName: 'Küche',
    candidateStationIds: ['station-kueche'],
    productionMinutes: 8,
    isSoldOut: false,
    isNoLongerOnTheMenu: false,
    isNoLongerPreparedAtItsStation: false,
    ...overrides,
  }
}

function beer(overrides: Partial<BasketLineView> = {}): BasketLineView {
  return line({
    catalogItemId: 'item-bier',
    name: 'Bier',
    stationId: 'station-theke',
    stationName: 'Theke innen',
    candidateStationIds: ['station-theke'],
    productionMinutes: null,
    ...overrides,
  })
}

function alwaysTogether(): DeliveryMode {
  return 'together'
}

describe('what each station of an order is asked to do', () => {
  it('names one station per part of the order, in the order the parts stand on the screen', () => {
    const deliveries = stationDeliveries([line(), beer()], QUEUES, alwaysTogether)

    expect(deliveries.map((delivery) => delivery.stationId)).toEqual([
      'station-kueche',
      'station-theke',
    ])
    expect(deliveries.map((delivery) => delivery.stationName)).toEqual(['Küche', 'Theke innen'])
  })

  it('carries the mode the server chose for that station and nothing from the other one', () => {
    const chosen: Record<string, DeliveryMode> = { 'station-theke': 'asItComes' }

    const deliveries = stationDeliveries([line(), beer()], QUEUES, (stationId) =>
      chosen[stationId] ?? 'together',
    )

    expect(deliveries.map((delivery) => delivery.deliveryMode)).toEqual(['together', 'asItComes'])
  })

  it('hands everything out together while the line still waits for its station', () => {
    const deliveries = stationDeliveries(
      [line({ stationId: null, candidateStationIds: ['station-kueche', 'station-theke'] })],
      QUEUES,
      () => 'asItComes',
    )

    expect(deliveries[0].stationId).toBeNull()
    expect(deliveries[0].deliveryMode).toBe('together')
  })
})

describe('how long a station says its part will take', () => {
  it('takes the slowest item of the part when the station hands it out together', () => {
    const deliveries = stationDeliveries(
      [line(), line({ catalogItemId: 'item-pommes', name: 'Pommes', productionMinutes: 3 })],
      QUEUES,
      alwaysTogether,
    )

    expect(deliveries[0].minutes).toBe(20)
  })

  it('names no time for the part when each item goes out on its own', () => {
    const deliveries = stationDeliveries([line()], QUEUES, () => 'asItComes')

    expect(deliveries[0].minutes).toBeNull()
  })

  it('names no time for a part whose items nobody gave a preparation time', () => {
    const deliveries = stationDeliveries([beer()], QUEUES, alwaysTogether)

    expect(deliveries[0].minutes).toBeNull()
  })

  it('takes the slowest item that has a time when the rest of the part has none', () => {
    const deliveries = stationDeliveries(
      [line(), line({ catalogItemId: 'item-brot', name: 'Brot', productionMinutes: null })],
      QUEUES,
      alwaysTogether,
    )

    expect(deliveries[0].minutes).toBe(20)
  })

  it('names no time while the part has no station to be queued at', () => {
    const deliveries = stationDeliveries(
      [line({ stationId: null, candidateStationIds: ['station-kueche', 'station-theke'] })],
      QUEUES,
      alwaysTogether,
    )

    expect(deliveries[0].minutes).toBeNull()
  })

  it('leaves out an item that cannot be ordered, because the part will not carry it', () => {
    const deliveries = stationDeliveries(
      [
        line({ productionMinutes: 4 }),
        line({ catalogItemId: 'item-pommes', name: 'Pommes', productionMinutes: 8, isSoldOut: true }),
      ],
      QUEUES,
      alwaysTogether,
    )

    expect(deliveries[0].minutes).toBe(16)
  })

  it('leaves out an item that has left the menu, for the same reason', () => {
    const deliveries = stationDeliveries(
      [
        line({ productionMinutes: 4 }),
        line({
          catalogItemId: 'item-currywurst',
          name: 'Currywurst',
          productionMinutes: 8,
          isNoLongerOnTheMenu: true,
        }),
      ],
      QUEUES,
      alwaysTogether,
    )

    expect(deliveries[0].minutes).toBe(16)
  })

  it('names no time for a part where nothing at all can be ordered', () => {
    const deliveries = stationDeliveries(
      [line({ productionMinutes: 8, isSoldOut: true })],
      QUEUES,
      alwaysTogether,
    )

    expect(deliveries[0].minutes).toBeNull()
  })

  it('counts a station the laptop said nothing about as having nothing queued', () => {
    const deliveries = stationDeliveries(
      [line({ stationId: 'station-grill', candidateStationIds: ['station-grill'] })],
      QUEUES,
      alwaysTogether,
    )

    expect(deliveries[0].minutes).toBe(8)
  })
})
