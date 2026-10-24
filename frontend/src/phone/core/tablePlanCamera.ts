import type { AreaShapeGeometry } from '../../shared/api/generatedSchemas'
import { TABLE_PLAN_DRAWING_WIDTH, lowestEdgeOf, type PlanPoint } from '../../shared/core/tablePlanDrawing'

export const ROOM_BELOW_THE_LOWEST_SHAPE = 20

export const DEEPEST_ZOOM = 4

export interface PlanCamera {
  scale: number
  x: number
  y: number
}

export interface PlanViewport {
  width: number
  height: number
}

export interface Pinch {
  centre: PlanPoint
  distance: number
}

export function waiterDrawingLength(geometries: readonly AreaShapeGeometry[]): number {
  return lowestEdgeOf(geometries) + ROOM_BELOW_THE_LOWEST_SHAPE
}

export function cameraFittingTheWidth(viewport: PlanViewport): PlanCamera {
  return { scale: viewport.width / TABLE_PLAN_DRAWING_WIDTH, x: 0, y: 0 }
}

export function clampedPosition(
  position: PlanPoint,
  scale: number,
  viewport: PlanViewport,
  drawingLength: number,
): PlanPoint {
  const leftmost = Math.min(0, viewport.width - TABLE_PLAN_DRAWING_WIDTH * scale)
  const topmost = Math.min(0, viewport.height - drawingLength * scale)
  return {
    x: Math.min(0, Math.max(leftmost, position.x)),
    y: Math.min(0, Math.max(topmost, position.y)),
  }
}

export function pinchedCamera(
  camera: PlanCamera,
  from: Pinch,
  to: Pinch,
  viewport: PlanViewport,
  drawingLength: number,
): PlanCamera {
  const fitting = viewport.width / TABLE_PLAN_DRAWING_WIDTH
  const scale = Math.min(fitting * DEEPEST_ZOOM, Math.max(fitting, (camera.scale * to.distance) / from.distance))
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
