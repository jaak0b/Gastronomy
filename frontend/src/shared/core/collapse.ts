export interface CollapsedLine<TLine> {
  key: string
  line: TLine
  quantity: number
}

export function groupKeepingFirstSeenOrder<TItem, TGroup>(
  items: readonly TItem[],
  keyOf: (item: TItem) => string,
  startGroup: (item: TItem, key: string) => TGroup,
  addToGroup: (group: TGroup, item: TItem) => TGroup,
): TGroup[] {
  const groups: TGroup[] = []
  const positionByKey = new Map<string, number>()

  for (const item of items) {
    const key = keyOf(item)
    const position = positionByKey.get(key)
    if (position === undefined) {
      positionByKey.set(key, groups.length)
      groups.push(startGroup(item, key))
      continue
    }
    groups[position] = addToGroup(groups[position], item)
  }

  return groups
}

export function mergeLinesWithSameArticleAndNote<TLine>(
  lines: readonly TLine[],
  nameOf: (line: TLine) => string,
  noteOf: (line: TLine) => string | null,
): CollapsedLine<TLine>[] {
  return groupKeepingFirstSeenOrder<TLine, CollapsedLine<TLine>>(
    lines,
    (line) => JSON.stringify([nameOf(line), noteOf(line) ?? '']),
    (line, key) => ({ key, line, quantity: 1 }),
    (collapsed) => ({ ...collapsed, quantity: collapsed.quantity + 1 }),
  )
}
