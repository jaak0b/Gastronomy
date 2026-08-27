import { describe, expect, it } from 'vitest'
import { isLineFlaggedSoldOut, itemState } from '../../src/core/catalogItemState'
import type { Catalog, CatalogItem } from '../../src/core/apiTypes'
import type { BasketLineView } from '../../src/core/basket'

function item(id: string, isAvailable: boolean): CatalogItem {
  return {
    id,
    name: 'Bratwurst',
    categoryName: 'Essen',
    priceCents: 350,
    sortOrder: 1,
    isAvailable,
    locationIds: ['location-kueche'],
  }
}

function catalogWith(items: CatalogItem[]): Catalog {
  return {
    version: '7',
    categories: [{ name: 'Essen', sortOrder: 1 }],
    items,
    locations: [{ id: 'location-kueche', name: 'Kueche', sortOrder: 1 }],
    tableSuggestions: [],
  }
}

function line(catalogItemId: string): BasketLineView {
  return {
    catalogItemId,
    name: 'Bratwurst',
    unitPriceCents: 350,
    quantity: 1,
    note: null,
    productionLocationId: null,
    candidateLocationIds: ['location-kueche'],
    isSoldOut: false,
    isNoLongerOnTheMenu: false,
  }
}

describe('itemState', () => {
  it('reads an item the kitchen still has as available', () => {
    const state = itemState(item('item-1', true))

    expect(state).toBe('available')
  })

  it('reads an item the kitchen has run out of as sold out', () => {
    const state = itemState(item('item-1', false))

    expect(state).toBe('soldOut')
  })
})

describe('isLineFlaggedSoldOut', () => {
  it('flags a line whose item sold out while the basket was open', () => {
    const flagged = isLineFlaggedSoldOut(line('item-1'), catalogWith([item('item-1', false)]))

    expect(flagged).toBe(true)
  })

  it('leaves a line whose item is still available unflagged', () => {
    const flagged = isLineFlaggedSoldOut(line('item-1'), catalogWith([item('item-1', true)]))

    expect(flagged).toBe(false)
  })

  it('leaves a line unflagged when its item is no longer on the menu at all', () => {
    const flagged = isLineFlaggedSoldOut(line('item-gone'), catalogWith([item('item-1', true)]))

    expect(flagged).toBe(false)
  })
})
