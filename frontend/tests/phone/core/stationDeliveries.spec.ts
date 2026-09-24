import { describe, expect, it } from 'vitest'
import { stationDeliveries } from '../../../src/phone/core/stationDeliveries'
import type { BasketLineView } from '../../../src/phone/core/basket'
import { DeliveryMode, StationQuoteView } from '../../../src/shared/api/generatedSchemas'

const QUOTE: StationQuoteView[] = [
  { stationId: 'station-kueche', readyInMinutes: 23 },
  { stationId: 'station-theke', readyInMinutes: null },
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
    ...overrides,
  })
}

function alwaysTogether(): DeliveryMode {
  return 'together'
}

describe('what each station of an order is asked to do', () => {
  it('names one station per part of the order, in the order the parts stand on the screen', () => {
    const deliveries = stationDeliveries([line(), beer()], QUOTE, alwaysTogether)

    expect(deliveries.map((delivery) => delivery.stationId)).toEqual([
      'station-kueche',
      'station-theke',
    ])
    expect(deliveries.map((delivery) => delivery.stationName)).toEqual(['Küche', 'Theke innen'])
  })

  it('carries the mode the server chose for that station and nothing from the other one', () => {
    const chosen: Record<string, DeliveryMode> = { 'station-theke': 'asItComes' }

    const deliveries = stationDeliveries([line(), beer()], QUOTE, (stationId) =>
      chosen[stationId] ?? 'together',
    )

    expect(deliveries.map((delivery) => delivery.deliveryMode)).toEqual(['together', 'asItComes'])
  })

  it('hands everything out together while the line still waits for its station', () => {
    const deliveries = stationDeliveries(
      [line({ stationId: null, candidateStationIds: ['station-kueche', 'station-theke'] })],
      QUOTE,
      () => 'asItComes',
    )

    expect(deliveries[0].stationId).toBeNull()
    expect(deliveries[0].deliveryMode).toBe('together')
  })
})

describe('how long a station says its part will take', () => {
  it('names the time the laptop calculated for the station', () => {
    const deliveries = stationDeliveries([line()], QUOTE, alwaysTogether)

    expect(deliveries[0].minutes).toBe(23)
  })

  it('names no time for the part when each item goes out on its own', () => {
    const deliveries = stationDeliveries([line()], QUOTE, () => 'asItComes')

    expect(deliveries[0].minutes).toBeNull()
  })

  it('keeps the calculated time of the station even when the part goes out item by item', () => {
    const deliveries = stationDeliveries([line()], QUOTE, () => 'asItComes')

    expect(deliveries[0].stationMinutes).toBe(23)
  })

  it('names no time for a station the laptop could not calculate', () => {
    const deliveries = stationDeliveries([beer()], QUOTE, alwaysTogether)

    expect(deliveries[0].minutes).toBeNull()
  })

  it('names no time while the part has no station yet', () => {
    const deliveries = stationDeliveries(
      [line({ stationId: null, candidateStationIds: ['station-kueche', 'station-theke'] })],
      QUOTE,
      alwaysTogether,
    )

    expect(deliveries[0].minutes).toBeNull()
  })

  it('names no time for a station the laptop said nothing about', () => {
    const deliveries = stationDeliveries(
      [line({ stationId: 'station-grill', candidateStationIds: ['station-grill'] })],
      QUOTE,
      alwaysTogether,
    )

    expect(deliveries[0].minutes).toBeNull()
  })
})
