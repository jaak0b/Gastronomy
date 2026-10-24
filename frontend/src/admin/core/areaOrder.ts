import type { AdminAreaView } from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'

export type AreaMove = 'up' | 'down'

interface AreaSortOrderChange {
  area: AdminAreaView
  sortOrder: number
}

function neighbourPosition(position: number, move: AreaMove): number {
  switch (move) {
    case 'up':
      return position - 1
    case 'down':
      return position + 1
    default:
      return assertNever(move)
  }
}

export function sortOrderChangesForMoving(
  areas: readonly AdminAreaView[],
  areaId: string,
  move: AreaMove,
): AreaSortOrderChange[] {
  const position = areas.findIndex((area) => area.areaId === areaId)
  const neighbour = neighbourPosition(position, move)
  if (position === -1 || neighbour < 0 || neighbour >= areas.length) {
    return []
  }
  const reordered = [...areas]
  reordered[position] = areas[neighbour]
  reordered[neighbour] = areas[position]
  return reordered
    .map((area, index) => ({ area, sortOrder: index + 1 }))
    .filter((change) => change.area.sortOrder !== change.sortOrder)
}

export function sortOrderForANewArea(areas: readonly AdminAreaView[]): number {
  return Math.max(0, ...areas.map((area) => area.sortOrder)) + 1
}
