import { describe, expect, it } from 'vitest'
import { candidateStations, needsStationChoice } from '../../src/core/routingPreview'
import type { CatalogItem } from '../../src/core/apiTypes'

function itemWith(stationIds: string[]): CatalogItem {
  return {
    id: 'item-1',
    name: 'Bier',
    categoryName: 'Getraenke',
    priceCents: 420,
    sortOrder: 1,
    isAvailable: true,
    stationIds,
  }
}

describe('candidateStations', () => {
  it('names the one station that can prepare the item', () => {
    const candidates = candidateStations(itemWith(['station-kueche']))

    expect(candidates).toEqual(['station-kueche'])
  })

  it('names every station that can prepare the item', () => {
    const candidates = candidateStations(
      itemWith(['station-theke-innen', 'station-theke-aussen']),
    )

    expect(candidates).toEqual(['station-theke-innen', 'station-theke-aussen'])
  })

  it('names no station for an item nobody was assigned to prepare', () => {
    const candidates = candidateStations(itemWith([]))

    expect(candidates).toEqual([])
  })
})

describe('needsStationChoice', () => {
  it('never asks when exactly one station can prepare the item', () => {
    const asks = needsStationChoice(itemWith(['station-kueche']))

    expect(asks).toBe(false)
  })

  it('asks when two stations can prepare the item', () => {
    const asks = needsStationChoice(itemWith(['station-theke-innen', 'station-theke-aussen']))

    expect(asks).toBe(true)
  })

  it('never asks when no station can prepare the item', () => {
    const asks = needsStationChoice(itemWith([]))

    expect(asks).toBe(false)
  })
})
