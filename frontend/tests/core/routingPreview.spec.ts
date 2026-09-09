import { describe, expect, it } from 'vitest'
import {
  candidateStations,
  needsStationChoice,
  routedStationId,
  stationStillPreparesIt,
} from '../../src/core/routingPreview'
import type { CatalogItem } from '../../src/core/apiTypes'

function item(stationIds: string[]): CatalogItem {
  return {
    id: 'item-1',
    name: 'Bratwurst',
    categoryId: 'category-essen',
    priceCents: 350,
    sortOrder: 1,
    isAvailable: true,
    stationIds,
    productionMinutes: null,
  }
}

describe('needsStationChoice', () => {
  it('asks nothing for an item with exactly one station', () => {
    expect(needsStationChoice(item(['station-kueche']))).toBe(false)
  })

  it('asks for a choice for an item two stations could prepare', () => {
    expect(needsStationChoice(item(['station-kueche', 'station-grill']))).toBe(true)
  })

  it('asks nothing for an item with no station, because there is nothing to choose', () => {
    expect(needsStationChoice(item([]))).toBe(false)
  })
})

describe('candidateStations', () => {
  it('lists the stations of the item without touching the item', () => {
    const bratwurst = item(['station-kueche'])

    const candidates = candidateStations(bratwurst)
    candidates.push('station-grill')

    expect(bratwurst.stationIds).toEqual(['station-kueche'])
  })
})

describe('routedStationId, where a line goes', () => {
  it('goes to the station the server chose', () => {
    expect(
      routedStationId({ stationId: 'station-grill', candidateStationIds: ['station-kueche', 'station-grill'] }),
    ).toBe('station-grill')
  })

  it('goes to the only candidate when nothing was chosen', () => {
    expect(routedStationId({ stationId: null, candidateStationIds: ['station-kueche'] })).toBe(
      'station-kueche',
    )
  })

  it('goes nowhere yet when several candidates are open and nothing was chosen', () => {
    expect(
      routedStationId({ stationId: null, candidateStationIds: ['station-kueche', 'station-grill'] }),
    ).toBeNull()
  })
})

describe('stationStillPreparesIt, after the item list changed under an order', () => {
  it('holds while the station the waiter picked still prepares the item', () => {
    expect(
      stationStillPreparesIt({
        stationId: 'station-grill',
        candidateStationIds: ['station-kueche', 'station-grill'],
      }),
    ).toBe(true)
  })

  it('fails once the item was taken away from the station the waiter picked', () => {
    expect(
      stationStillPreparesIt({
        stationId: 'station-grill',
        candidateStationIds: ['station-kueche'],
      }),
    ).toBe(false)
  })

  it('fails for a line whose item is prepared nowhere any more', () => {
    expect(stationStillPreparesIt({ stationId: 'station-grill', candidateStationIds: [] })).toBe(
      false,
    )
  })

  it('holds where the waiter picked nothing, because the item routes itself', () => {
    expect(stationStillPreparesIt({ stationId: null, candidateStationIds: ['station-kueche'] })).toBe(
      true,
    )
  })

  it('holds where the waiter picked nothing and the item lost its stations, because that line names no station to contradict', () => {
    expect(stationStillPreparesIt({ stationId: null, candidateStationIds: [] })).toBe(true)
  })
})
