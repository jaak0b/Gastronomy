import type { AdminAreaView, AdminTableView, AreaShapeGeometry } from '../../shared/api/generatedSchemas'
import { TABLE_PLAN_DRAWING_WIDTH, cornersOf, lowestEdgeOf } from '../../shared/core/tablePlanDrawing'
import { SNAP_GRID } from './tablePlanSnapping'

const ROOM_BELOW_THE_LOWEST_SHAPE = 200

const SHORTEST_SHAPE_SIDE = 20

const MEASURING_TOLERANCE = 1e-6

interface ShapeSize {
  width: number
  height: number
}

export const NEW_TABLE_SIZE: ShapeSize = { width: 80, height: 40 }

export const NEW_STATION_SIZE: ShapeSize = { width: 120, height: 40 }

export function drawnTablesOf(area: AdminAreaView): AdminTableView[] {
  return area.tables.filter((table) => !table.isHidden)
}

export function drawnGeometriesOf(area: AdminAreaView): AreaShapeGeometry[] {
  return [
    ...drawnTablesOf(area).map((table) => table.geometry),
    ...area.stationShapes.map((shape) => shape.geometry),
  ]
}

export function geometriesOfEveryTableAndStationOf(area: AdminAreaView): AreaShapeGeometry[] {
  return [...area.tables.map((table) => table.geometry), ...area.stationShapes.map((shape) => shape.geometry)]
}

export function geometryBelowTheLowestShape(
  shapes: readonly AreaShapeGeometry[],
  size: ShapeSize,
): AreaShapeGeometry {
  return {
    x: SNAP_GRID,
    y: lowestEdgeOf(shapes) + SNAP_GRID,
    width: size.width,
    height: size.height,
    rotation: 0,
  }
}

export function fitsTheDrawing(geometry: AreaShapeGeometry): boolean {
  return (
    geometry.width >= SHORTEST_SHAPE_SIDE
    && geometry.height >= SHORTEST_SHAPE_SIDE
    && cornersOf(geometry).every(
      (corner) =>
        corner.x >= -MEASURING_TOLERANCE
        && corner.x <= TABLE_PLAN_DRAWING_WIDTH + MEASURING_TOLERANCE
        && corner.y >= -MEASURING_TOLERANCE,
    )
  )
}

export function editorDrawingLength(shapes: readonly AreaShapeGeometry[], visibleLength: number): number {
  return Math.max(lowestEdgeOf(shapes) + ROOM_BELOW_THE_LOWEST_SHAPE, visibleLength)
}
