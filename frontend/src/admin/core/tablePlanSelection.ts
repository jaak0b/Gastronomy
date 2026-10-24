import type { AdminAreaView } from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'

export type ShapeSelection = { kind: 'table'; tableId: string } | { kind: 'station'; stationId: string }

export function shapeNodeIdOf(selection: ShapeSelection): string {
  switch (selection.kind) {
    case 'table':
      return `table-${selection.tableId}`
    case 'station':
      return `station-${selection.stationId}`
    default:
      return assertNever(selection)
  }
}

export function isDrawnIn(area: AdminAreaView, selection: ShapeSelection): boolean {
  switch (selection.kind) {
    case 'table':
      return area.tables.some((table) => table.tableId === selection.tableId)
    case 'station':
      return area.stationShapes.some((shape) => shape.stationId === selection.stationId)
    default:
      return assertNever(selection)
  }
}
