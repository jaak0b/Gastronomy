export interface CollapsedLine<TLine> {
  key: string
  line: TLine
  quantity: number
}

export function mergeLinesWithSameArticleAndNote<TLine>(
  lines: readonly TLine[],
  nameOf: (line: TLine) => string,
  noteOf: (line: TLine) => string | null,
): CollapsedLine<TLine>[] {
  const collapsed: CollapsedLine<TLine>[] = []
  const positionByKey = new Map<string, number>()

  for (const line of lines) {
    const key = JSON.stringify([nameOf(line), noteOf(line) ?? ''])
    const position = positionByKey.get(key)
    if (position === undefined) {
      positionByKey.set(key, collapsed.length)
      collapsed.push({ key, line, quantity: 1 })
      continue
    }
    collapsed[position] = {
      key: collapsed[position].key,
      line: collapsed[position].line,
      quantity: collapsed[position].quantity + 1,
    }
  }

  return collapsed
}
