import type { Catalog, CatalogItem } from './apiTypes'
import { findCatalogItem, type BasketLineView } from './basket'
import { assertNever } from './assertNever'

export type CatalogItemState = 'available' | 'soldOut'

export function itemState(item: CatalogItem): CatalogItemState {
  return item.isAvailable ? 'available' : 'soldOut'
}

export function isLineFlaggedSoldOut(line: BasketLineView, catalog: Catalog): boolean {
  const item = findCatalogItem(catalog, line.catalogItemId)
  if (item === null) {
    return false
  }
  const state = itemState(item)
  switch (state) {
    case 'available':
      return false
    case 'soldOut':
      return true
    default:
      return assertNever(state)
  }
}
