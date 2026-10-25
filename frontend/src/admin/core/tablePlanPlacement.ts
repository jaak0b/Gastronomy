import type { AdminAreaView, AdminTableView, AreaShapeGeometry } from '../../shared/api/generatedSchemas'
import {
  TABLE_PLAN_DRAWING_WIDTH,
  cornersOf,
  lowestEdgeOf,
  type PlanPoint,
} from '../../shared/core/tablePlanDrawing'
import { SNAP_GRID } from './tablePlanSnapping'

const ROOM_BELOW_THE_LOWEST_SHAPE = 200

const SHORTEST_SHAPE_SIDE = 20

const MEASURING_TOLERANCE = 1e-6

interface ShapeSize {
  width: number
  height: number
}

export interface TableCopy {
  sourceTableId: string
  geometry: AreaShapeGeometry
}

interface CopyBounds {
  left: number
  right: number
  top: number
}

export const NEW_TABLE_SIZE: ShapeSize = { width: 80, height: 40 }

export const NEW_STATION_SIZE: ShapeSize = { width: 120, height: 40 }

export function drawnTablesOf(area: AdminAreaView): AdminTableView[] {
  return area.tables.filter((table) => !table.isHidden)
}

export function geometryForANewShapeIn(area: AdminAreaView, size: ShapeSize): AreaShapeGeometry {
  const everyTableAndStation = [
    ...area.tables.map((table) => table.geometry),
    ...area.stationShapes.map((shape) => shape.geometry),
  ]
  return {
    x: SNAP_GRID,
    y: lowestEdgeOf(everyTableAndStation) + SNAP_GRID,
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

export function pastedCopiesBeside(
  copies: readonly TableCopy[],
  sourceAreaId: string,
  targetAreaId: string,
): TableCopy[] {
  const moved = copies.map((copy) => ({
    sourceTableId: copy.sourceTableId,
    geometry:
      sourceAreaId === targetAreaId
        ? { ...copy.geometry, x: copy.geometry.x + SNAP_GRID, y: copy.geometry.y + SNAP_GRID }
        : { ...copy.geometry },
  }))
  return shiftedInsideTheDrawingWidth(moved)
}

export function pastedCopiesAt(copies: readonly TableCopy[], at: PlanPoint): TableCopy[] {
  if (copies.length === 0) {
    return []
  }
  const bounds = copyBoundsOf(copies)
  const moved = copies.map((copy) => ({
    sourceTableId: copy.sourceTableId,
    geometry: {
      ...copy.geometry,
      x: copy.geometry.x + at.x - bounds.left,
      y: copy.geometry.y + at.y - bounds.top,
    },
  }))
  return shiftedInsideTheDrawingWidth(moved)
}

function copyBoundsOf(copies: readonly TableCopy[]): CopyBounds {
  const corners = copies.flatMap((copy) => cornersOf(copy.geometry))
  return {
    left: Math.min(...corners.map((corner) => corner.x)),
    right: Math.max(...corners.map((corner) => corner.x)),
    top: Math.min(...corners.map((corner) => corner.y)),
  }
}

function shiftedInsideTheDrawingWidth(copies: TableCopy[]): TableCopy[] {
  if (copies.length === 0) {
    return []
  }
  const bounds = copyBoundsOf(copies)
  const shiftLeft = Math.min(
    Math.max(0, bounds.right - TABLE_PLAN_DRAWING_WIDTH),
    Math.max(0, bounds.left),
  )
  if (shiftLeft === 0) {
    return copies
  }
  return copies.map((copy) => ({
    sourceTableId: copy.sourceTableId,
    geometry: { ...copy.geometry, x: copy.geometry.x - shiftLeft },
  }))
}
