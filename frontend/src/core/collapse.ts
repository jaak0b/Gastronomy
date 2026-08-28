export interface CollapsedLine<TLine> {
  line: TLine
  quantity: number
}

export function collapseLines<TLine>(
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
      collapsed.push({ line, quantity: 1 })
      continue
    }
    collapsed[position] = { line: collapsed[position].line, quantity: collapsed[position].quantity + 1 }
  }

  return collapsed
}
