import type { AdminAreaView, AdminTableView, AreaShapeGeometry } from '../../shared/api/generatedSchemas'
import {
  TABLE_PLAN_DRAWING_WIDTH,
  cornersOf,
  lowestEdgeOf,
  type PlanPoint,
  type PlanSize,
} from '../../shared/core/tablePlanDrawing'
import { compareNames } from '../../shared/core/compareNames'
import { SNAP_GRID } from './tablePlanSnapping'

const ROOM_BELOW_THE_LOWEST_SHAPE = 200

const SHORTEST_SHAPE_SIDE = 20

const MEASURING_TOLERANCE = 1e-6

const DRAWINGS_ACROSS_THE_FRAME_AT_THE_FIRST_STEP = 3

const ZOOM_FACTOR_PER_STEP = 1.25

const SMALLEST_ZOOM_STEP = -3

const LARGEST_ZOOM_STEP = 8

export const FIRST_ZOOM_STEP = 0

export interface ShownDrawing {
  view: PlanSize
  drawing: PlanSize
}

export interface ScrolledDrawing extends ShownDrawing {
  scroll: PlanPoint
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

export const NEW_TABLE_SIZE: PlanSize = { width: 80, height: 40 }

export const NEW_STATION_SIZE: PlanSize = { width: 120, height: 40 }

export function drawnTablesOf(area: AdminAreaView): AdminTableView[] {
  return area.tables.filter((table) => !table.isHidden)
}

export function waitersSeeATable(areas: readonly AdminAreaView[]): boolean {
  return areas.some((area) => !area.isHidden && drawnTablesOf(area).length > 0)
}

export function hiddenTablesOf(area: AdminAreaView): AdminTableView[] {
  return area.tables
    .filter((table) => table.isHidden)
    .sort((left, right) => compareNames(left.name, right.name))
}

export function editorScale(frameWidth: number, zoomStep: number): number {
  const scaleAtTheFirstStep = frameWidth / DRAWINGS_ACROSS_THE_FRAME_AT_THE_FIRST_STEP / TABLE_PLAN_DRAWING_WIDTH
  return scaleAtTheFirstStep * ZOOM_FACTOR_PER_STEP ** zoomStep
}

export function canZoomIn(zoomStep: number): boolean {
  return zoomStep < LARGEST_ZOOM_STEP
}

export function canZoomOut(zoomStep: number): boolean {
  return zoomStep > SMALLEST_ZOOM_STEP
}

export function scrollKeepingTheCentre(before: ScrolledDrawing, after: ShownDrawing): PlanPoint {
  const centreBefore = {
    x: before.scroll.x + before.view.width / 2 - centringGap(before.view.width, before.drawing.width),
    y: before.scroll.y + before.view.height / 2,
  }
  const centreAfter = {
    x: (centreBefore.x * after.drawing.width) / before.drawing.width,
    y: (centreBefore.y * after.drawing.height) / before.drawing.height,
  }
  return {
    x: scrollInside(
      centreAfter.x - after.view.width / 2 + centringGap(after.view.width, after.drawing.width),
      after.view.width,
      after.drawing.width,
    ),
    y: scrollInside(centreAfter.y - after.view.height / 2, after.view.height, after.drawing.height),
  }
}

function centringGap(viewLength: number, drawingLength: number): number {
  return Math.max(0, (viewLength - drawingLength) / 2)
}

function scrollInside(wanted: number, viewLength: number, drawingLength: number): number {
  return Math.max(0, Math.min(wanted, drawingLength - viewLength))
}

export function geometryForANewShapeIn(area: AdminAreaView, size: PlanSize): AreaShapeGeometry {
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

export function editorDrawingLength(shapes: readonly AreaShapeGeometry[]): number {
  return lowestEdgeOf(shapes) + ROOM_BELOW_THE_LOWEST_SHAPE
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
