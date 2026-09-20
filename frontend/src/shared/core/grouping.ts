export interface CategoryGroup<TCategory, TItem> {
  category: TCategory
  items: TItem[]
}

export function groupByCategory<TCategory, TItem>(
  categories: readonly TCategory[],
  items: readonly TItem[],
  categoryIdOf: (category: TCategory) => string,
  itemCategoryIdOf: (item: TItem) => string,
  itemNameOf: (item: TItem) => string,
): CategoryGroup<TCategory, TItem>[] {
  return categories.map((category) => ({
    category,
    items: items
      .filter((item) => itemCategoryIdOf(item) === categoryIdOf(category))
      .sort((left, right) => itemNameOf(left).localeCompare(itemNameOf(right))),
  }))
}
