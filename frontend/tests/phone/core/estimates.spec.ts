import { describe, expect, it } from 'vitest'
import {
  estimateRangeForItem,
  quoteLinesFor,
  readyInMinutesAt,
} from '../../../src/phone/core/estimates'
import { ItemEstimateView } from '../../../src/shared/api/generatedSchemas'
import type { BasketLineView } from '../../../src/phone/core/basket'

const ESTIMATES: ItemEstimateView[] = [
  { catalogItemId: 'item-bratwurst', stationId: 'station-kueche', readyInMinutes: 48 },
  { catalogItemId: 'item-bratwurst', stationId: 'station-grill', readyInMinutes: 96 },
  { catalogItemId: 'item-bratwurst', stationId: 'station-zelt', readyInMinutes: 60 },
  { catalogItemId: 'item-pommes', stationId: 'station-kueche', readyInMinutes: 12 },
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

describe('estimateRangeForItem, what the item list shows', () => {
  it('spans the fastest and the slowest station the laptop named for the item', () => {
    expect(estimateRangeForItem(ESTIMATES, 'item-bratwurst')).toEqual({ min: 48, max: 96 })
  })

  it('shows one value when only one station makes the item', () => {
    expect(estimateRangeForItem(ESTIMATES, 'item-pommes')).toEqual({ min: 12, max: 12 })
  })

  it('shows nothing for an item the laptop gave no time for', () => {
    expect(estimateRangeForItem(ESTIMATES, 'item-bier')).toBeNull()
  })
})

describe('readyInMinutesAt, what one station button promises', () => {
  it('names the time the laptop gave for the item at that station', () => {
    expect(readyInMinutesAt(ESTIMATES, 'item-bratwurst', 'station-grill')).toBe(96)
  })

  it('names nothing for a station the laptop gave no time for', () => {
    expect(readyInMinutesAt(ESTIMATES, 'item-pommes', 'station-grill')).toBeNull()
  })
})

describe('quoteLinesFor, what the review screen asks the laptop to calculate', () => {
  it('counts the units of one article at one station into one line whatever their notes say', () => {
    const lines = [line(), line({ note: 'ohne Senf' }), line()]

    expect(quoteLinesFor(lines)).toEqual([
      { catalogItemId: 'item-bratwurst', stationId: 'station-kueche', units: 3 },
    ])
  })

  it('keeps one article at two stations apart', () => {
    const lines = [line(), line({ stationId: 'station-grill', stationName: 'Grill' }), line()]

    expect(quoteLinesFor(lines)).toEqual([
      { catalogItemId: 'item-bratwurst', stationId: 'station-kueche', units: 2 },
      { catalogItemId: 'item-bratwurst', stationId: 'station-grill', units: 1 },
    ])
  })

  it('keeps two articles at one station apart', () => {
    const lines = [line(), line({ catalogItemId: 'item-pommes', name: 'Pommes' })]

    expect(quoteLinesFor(lines)).toEqual([
      { catalogItemId: 'item-bratwurst', stationId: 'station-kueche', units: 1 },
      { catalogItemId: 'item-pommes', stationId: 'station-kueche', units: 1 },
    ])
  })

  it('sends a line without a chosen station to the only station that makes it', () => {
    const lines = [line({ stationId: null, candidateStationIds: ['station-grill'] })]

    expect(quoteLinesFor(lines)).toEqual([
      { catalogItemId: 'item-bratwurst', stationId: 'station-grill', units: 1 },
    ])
  })

  it('leaves out a line whose station is still undecided', () => {
    const lines = [line({ stationId: null, candidateStationIds: ['station-kueche', 'station-grill'] })]

    expect(quoteLinesFor(lines)).toEqual([])
  })

  it('leaves out a line that can no longer be ordered', () => {
    const lines = [line({ isSoldOut: true }), line({ catalogItemId: 'item-pommes' })]

    expect(quoteLinesFor(lines)).toEqual([
      { catalogItemId: 'item-pommes', stationId: 'station-kueche', units: 1 },
    ])
  })
})
