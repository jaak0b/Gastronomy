import { CatalogItemView, CatalogView } from '../../shared/api/generatedSchemas'
import { findCatalogItem, type BasketLineView } from './basket'
import { assertNever } from '../../shared/core/assertNever'

export type CatalogItemState = 'available' | 'soldOut'

export function itemState(item: CatalogItemView): CatalogItemState {
  return item.isAvailable ? 'available' : 'soldOut'
}

export function isLineFlaggedSoldOut(line: BasketLineView, catalog: CatalogView): boolean {
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
