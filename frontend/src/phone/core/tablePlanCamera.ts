import type { AreaShapeGeometry } from '../../shared/api/generatedSchemas'
import {
  TABLE_PLAN_DRAWING_WIDTH,
  lowestEdgeOf,
  scaleFittingTheWholeDrawing,
  type PlanPoint,
  type PlanSize,
} from '../../shared/core/tablePlanDrawing'

export const ROOM_BELOW_THE_LOWEST_SHAPE = 20

export const DEEPEST_ZOOM = 4

export interface PlanCamera {
  scale: number
  x: number
  y: number
}

export interface Pinch {
  centre: PlanPoint
  distance: number
}

export function waiterDrawingLength(geometries: readonly AreaShapeGeometry[]): number {
  return lowestEdgeOf(geometries) + ROOM_BELOW_THE_LOWEST_SHAPE
}

function startingScale(
  viewport: PlanSize,
  drawingLength: number,
  narrowestScreenShowingTheWholePlan: number,
): number {
  if (viewport.width < narrowestScreenShowingTheWholePlan) {
    return viewport.width / TABLE_PLAN_DRAWING_WIDTH
  }
  return scaleFittingTheWholeDrawing(viewport, { width: TABLE_PLAN_DRAWING_WIDTH, height: drawingLength })
}

export function startingCamera(
  viewport: PlanSize,
  drawingLength: number,
  narrowestScreenShowingTheWholePlan: number,
): PlanCamera {
  const scale = startingScale(viewport, drawingLength, narrowestScreenShowingTheWholePlan)
  return { scale, ...clampedPosition({ x: 0, y: 0 }, scale, viewport, drawingLength) }
}

export function clampedPosition(
  position: PlanPoint,
  scale: number,
  viewport: PlanSize,
  drawingLength: number,
): PlanPoint {
  const drawnWidth = TABLE_PLAN_DRAWING_WIDTH * scale
  const leftmost = viewport.width - drawnWidth
  const topmost = Math.min(0, viewport.height - drawingLength * scale)
  return {
    x: drawnWidth < viewport.width ? (viewport.width - drawnWidth) / 2 : Math.min(0, Math.max(leftmost, position.x)),
    y: Math.min(0, Math.max(topmost, position.y)),
  }
}

export function pinchedCamera(
  camera: PlanCamera,
  from: Pinch,
  to: Pinch,
  viewport: PlanSize,
  drawingLength: number,
  narrowestScreenShowingTheWholePlan: number,
): PlanCamera {
  const starting = startingScale(viewport, drawingLength, narrowestScreenShowingTheWholePlan)
  const scale = Math.min(starting * DEEPEST_ZOOM, Math.max(starting, (camera.scale * to.distance) / from.distance))
  const pointUnderTheFingers = {
    x: (to.centre.x - camera.x) / camera.scale,
    y: (to.centre.y - camera.y) / camera.scale,
  }
  const wanted = {
    x: to.centre.x - pointUnderTheFingers.x * scale + (to.centre.x - from.centre.x),
    y: to.centre.y - pointUnderTheFingers.y * scale + (to.centre.y - from.centre.y),
  }
  return { scale, ...clampedPosition(wanted, scale, viewport, drawingLength) }
}

export function pinchOf(first: PlanPoint, second: PlanPoint): Pinch {
  return {
    centre: { x: (first.x + second.x) / 2, y: (first.y + second.y) / 2 },
    distance: Math.hypot(first.x - second.x, first.y - second.y),
  }
}
