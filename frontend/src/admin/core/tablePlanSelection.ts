import type { AdminAreaView } from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'
import { cornersOf } from '../../shared/core/tablePlanDrawing'
import { drawnTablesOf } from './tablePlanPlacement'
import type { PlanBox } from './tablePlanSnapping'

export type PlanShape = { kind: 'table'; tableId: string } | { kind: 'station'; stationId: string }

export type TablePlanSelection =
  | { kind: 'tables'; tableIds: readonly string[] }
  | { kind: 'station'; stationId: string }

export function nodeIdOfTable(tableId: string): string {
  return `table-${tableId}`
}

export function nodeIdOfStation(stationId: string): string {
  return `station-${stationId}`
}

export function shapeNodeIdOf(shape: PlanShape): string {
  switch (shape.kind) {
    case 'table':
      return nodeIdOfTable(shape.tableId)
    case 'station':
      return nodeIdOfStation(shape.stationId)
    default:
      return assertNever(shape)
  }
}

export function selectionOfTables(tableIds: readonly string[]): TablePlanSelection | null {
  const distinct = [...new Set(tableIds)]
  return distinct.length === 0 ? null : { kind: 'tables', tableIds: distinct }
}

export function selectedTableIds(selection: TablePlanSelection | null): readonly string[] {
  if (selection === null) {
    return []
  }
  switch (selection.kind) {
    case 'tables':
      return selection.tableIds
    case 'station':
      return []
    default:
      return assertNever(selection)
  }
}

export function isTableSelected(selection: TablePlanSelection | null, tableId: string): boolean {
  return selectedTableIds(selection).includes(tableId)
}

export function withTableToggled(
  selection: TablePlanSelection | null,
  tableId: string,
): TablePlanSelection | null {
  if (selection === null) {
    return selectionOfTables([tableId])
  }
  switch (selection.kind) {
    case 'tables':
      return selectionOfTables(
        selection.tableIds.includes(tableId)
          ? selection.tableIds.filter((selected) => selected !== tableId)
          : [...selection.tableIds, tableId],
      )
    case 'station':
      return selectionOfTables([tableId])
    default:
      return assertNever(selection)
  }
}

export function isDrawnIn(area: AdminAreaView, selection: TablePlanSelection): boolean {
  switch (selection.kind) {
    case 'tables':
      return selection.tableIds.every((tableId) =>
        drawnTablesOf(area).some((table) => table.tableId === tableId),
      )
    case 'station':
      return area.stationShapes.some((shape) => shape.stationId === selection.stationId)
    default:
      return assertNever(selection)
  }
}

export function tableIdsInMarquee(area: AdminAreaView, marquee: PlanBox): string[] {
  return drawnTablesOf(area)
    .filter((table) => {
      const corners = cornersOf(table.geometry)
      const left = Math.min(...corners.map((corner) => corner.x))
      const right = Math.max(...corners.map((corner) => corner.x))
      const top = Math.min(...corners.map((corner) => corner.y))
      const bottom = Math.max(...corners.map((corner) => corner.y))
      return (
        right >= marquee.x
        && left <= marquee.x + marquee.width
        && bottom >= marquee.y
        && top <= marquee.y + marquee.height
      )
    })
    .map((table) => table.tableId)
}
