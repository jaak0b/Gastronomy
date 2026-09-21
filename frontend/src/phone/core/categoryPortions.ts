import type { DraftOrder } from './draftCart'
import { CatalogItemView } from '../../shared/api/generatedSchemas'
import { countItemPortions } from './itemPositions'

export function countCategoryPortions(
  draft: DraftOrder,
  items: readonly CatalogItemView[],
  categoryId: string,
): number {
  return items
    .filter((item) => item.categoryId === categoryId)
    .reduce((portions, item) => portions + countItemPortions(draft, item.id), 0)
}
