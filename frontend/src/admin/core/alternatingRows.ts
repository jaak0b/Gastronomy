export interface PositionedRow<TItem> {
  item: TItem
  position: number
}

export function numberRowsAcrossGroups<TGroup extends { items: readonly unknown[] }>(
  groups: readonly TGroup[],
): (TGroup & { rows: PositionedRow<TGroup['items'][number]>[] })[] {
  const numbered: (TGroup & { rows: PositionedRow<TGroup['items'][number]>[] })[] = []
  let firstPosition = 0
  for (const group of groups) {
    numbered.push({
      ...group,
      rows: group.items.map((item, index) => ({ item, position: firstPosition + index })),
    })
    firstPosition += group.items.length
  }
  return numbered
}
