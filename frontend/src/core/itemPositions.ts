import type { CatalogItem, DraftLine, DraftOrder } from './apiTypes'
import { needsStationChoice } from './routingPreview'

export interface ItemPosition {
  index: number
  note: string | null
  hasAStationChoice: boolean
  stationName: string | null
}

interface NumberedLine {
  line: DraftLine
  index: number
}

function linesOfItem(draft: DraftOrder, catalogItemId: string): NumberedLine[] {
  return draft.lines
    .map((line, index) => ({ line, index }))
    .filter((entry) => entry.line.catalogItemId === catalogItemId)
}

export function portionsOfItem(draft: DraftOrder, catalogItemId: string): number {
  return linesOfItem(draft, catalogItemId).length
}

export function positionsForItem(
  draft: DraftOrder,
  item: CatalogItem,
  stationNameOf: (stationId: string) => string,
): ItemPosition[] {
  const hasAStationChoice = needsStationChoice(item)

  return linesOfItem(draft, item.id)
    .map((entry) => ({
      index: entry.index,
      note: entry.line.note,
      hasAStationChoice,
      stationName:
        hasAStationChoice && entry.line.stationId !== null
          ? stationNameOf(entry.line.stationId)
          : null,
    }))
}

export interface PositionGroup {
  note: string | null
  stationName: string | null
  indexes: number[]
}

export function groupPositions(positions: readonly ItemPosition[]): PositionGroup[] {
  const groups: PositionGroup[] = []
  const positionByKey = new Map<string, number>()

  for (const position of positions) {
    const key = JSON.stringify([position.note ?? '', position.stationName ?? ''])
    const found = positionByKey.get(key)
    if (found === undefined) {
      positionByKey.set(key, groups.length)
      groups.push({
        note: position.note,
        stationName: position.stationName,
        indexes: [position.index],
      })
      continue
    }
    groups[found].indexes.push(position.index)
  }

  return groups
}
