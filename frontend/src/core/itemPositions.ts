import type { CatalogItem, DraftOrder } from './apiTypes'
import { needsStationChoice } from './routingPreview'

export interface ItemPosition {
  index: number
  note: string | null
  hasAStationChoice: boolean
  stationName: string | null
}

export function positionsForItem(
  draft: DraftOrder,
  item: CatalogItem,
  stationNameOf: (stationId: string) => string,
): ItemPosition[] {
  const hasAStationChoice = needsStationChoice(item)

  return draft.lines
    .map((line, index) => ({ line, index }))
    .filter((entry) => entry.line.catalogItemId === item.id)
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
