import type { CatalogItem, DraftOrder } from './apiTypes'
import { portionsOfItem } from './itemPositions'

export function portionsOfCategory(
  draft: DraftOrder,
  items: readonly CatalogItem[],
  categoryName: string,
): number {
  return items
    .filter((item) => item.categoryName === categoryName)
    .reduce((portions, item) => portions + portionsOfItem(draft, item.id), 0)
}
