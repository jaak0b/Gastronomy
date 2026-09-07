import type { CatalogItem, DraftOrder } from './apiTypes'
import { portionsOfItem } from './itemPositions'

export function portionsOfCategory(
  draft: DraftOrder,
  items: readonly CatalogItem[],
  categoryId: string,
): number {
  return items
    .filter((item) => item.categoryId === categoryId)
    .reduce((portions, item) => portions + portionsOfItem(draft, item.id), 0)
}
