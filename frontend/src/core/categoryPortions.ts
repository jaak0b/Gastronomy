import type { CatalogItem, DraftOrder } from '../shared/api/apiTypes'
import { countItemPortions } from './itemPositions'

export function countCategoryPortions(
  draft: DraftOrder,
  items: readonly CatalogItem[],
  categoryId: string,
): number {
  return items
    .filter((item) => item.categoryId === categoryId)
    .reduce((portions, item) => portions + countItemPortions(draft, item.id), 0)
}
