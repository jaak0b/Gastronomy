import type { CatalogItem, DraftLine, DraftOrder } from '../../shared/api/apiTypes'
import { groupKeepingFirstSeenOrder } from '../../shared/core/collapse'
import { needsStationChoice } from './routingPreview'

export interface ItemPosition {
  index: number
  note: string | null
  hasAStationChoice: boolean
  stationId: string | null
  stationName: string | null
}

interface NumberedLine {
  line: DraftLine
  index: number
}

function numberedLinesForItem(draft: DraftOrder, catalogItemId: string): NumberedLine[] {
  return draft.lines
    .map((line, index) => ({ line, index }))
    .filter((entry) => entry.line.catalogItemId === catalogItemId)
}

export function countItemPortions(draft: DraftOrder, catalogItemId: string): number {
  return numberedLinesForItem(draft, catalogItemId).length
}

export function positionsForItem(
  draft: DraftOrder,
  item: CatalogItem,
  stationNameOf: (stationId: string) => string,
): ItemPosition[] {
  const hasAStationChoice = needsStationChoice(item)

  return numberedLinesForItem(draft, item.id)
    .map((entry) => ({
      index: entry.index,
      note: entry.line.note,
      hasAStationChoice,
      stationId: entry.line.stationId,
      stationName:
        hasAStationChoice && entry.line.stationId !== null
          ? stationNameOf(entry.line.stationId)
          : null,
    }))
}

export interface PositionGroup {
  note: string | null
  stationId: string | null
  stationName: string | null
  indexes: number[]
}

export function groupPositions(positions: readonly ItemPosition[]): PositionGroup[] {
  return groupKeepingFirstSeenOrder<ItemPosition, PositionGroup>(
    positions,
    (position) => JSON.stringify([position.note ?? '', position.stationId ?? '']),
    (position) => ({
      note: position.note,
      stationId: position.stationId,
      stationName: position.stationName,
      indexes: [position.index],
    }),
    (group, position) => ({ ...group, indexes: [...group.indexes, position.index] }),
  )
}
