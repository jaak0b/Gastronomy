export function idsOfEverySecondRow<TItem>(
  groups: readonly { items: readonly TItem[] }[],
  idOf: (item: TItem) => string,
): Set<string> {
  const rows = groups.flatMap((group) => group.items)
  return new Set(rows.filter((_row, position) => position % 2 === 1).map(idOf))
}
