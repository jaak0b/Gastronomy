export interface CategoryGroup<TItem> {
  name: string
  items: TItem[]
}

export function groupByCategory<TItem>(
  items: readonly TItem[],
  categoryOf: (item: TItem) => string,
  nameOf: (item: TItem) => string,
): CategoryGroup<TItem>[] {
  const byCategory = new Map<string, TItem[]>()

  for (const item of items) {
    const category = categoryOf(item)
    byCategory.set(category, [...(byCategory.get(category) ?? []), item])
  }

  return [...byCategory.entries()]
    .map(([name, grouped]) => ({
      name,
      items: [...grouped].sort((left, right) => nameOf(left).localeCompare(nameOf(right))),
    }))
    .sort((left, right) => left.name.localeCompare(right.name))
}
