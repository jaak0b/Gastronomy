import { describe, expect, it } from 'vitest'
import { candidateLocations, needsStationChoice } from '../../src/core/routingPreview'
import type { CatalogItem } from '../../src/core/apiTypes'

function itemWith(locationIds: string[]): CatalogItem {
  return {
    id: 'item-1',
    name: 'Bier',
    categoryName: 'Getraenke',
    priceCents: 420,
    sortOrder: 1,
    isAvailable: true,
    locationIds,
  }
}

describe('candidateLocations', () => {
  it('names the one station that can prepare the item', () => {
    const candidates = candidateLocations(itemWith(['location-kueche']))

    expect(candidates).toEqual(['location-kueche'])
  })

  it('names every station that can prepare the item', () => {
    const candidates = candidateLocations(
      itemWith(['location-theke-innen', 'location-theke-aussen']),
    )

    expect(candidates).toEqual(['location-theke-innen', 'location-theke-aussen'])
  })

  it('names no station for an item nobody was assigned to prepare', () => {
    const candidates = candidateLocations(itemWith([]))

    expect(candidates).toEqual([])
  })
})

describe('needsStationChoice', () => {
  it('never asks when exactly one station can prepare the item', () => {
    const asks = needsStationChoice(itemWith(['location-kueche']))

    expect(asks).toBe(false)
  })

  it('asks when two stations can prepare the item', () => {
    const asks = needsStationChoice(itemWith(['location-theke-innen', 'location-theke-aussen']))

    expect(asks).toBe(true)
  })

  it('never asks when no station can prepare the item', () => {
    const asks = needsStationChoice(itemWith([]))

    expect(asks).toBe(false)
  })
})
