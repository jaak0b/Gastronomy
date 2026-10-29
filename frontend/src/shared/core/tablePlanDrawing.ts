import type { AreaShapeGeometry } from '../api/generatedSchemas'

export const TABLE_PLAN_DRAWING_WIDTH = 400

export interface PlanPoint {
  x: number
  y: number
}

export interface PlanSize {
  width: number
  height: number
}

export interface PlanRectangle {
  x: number
  y: number
  width: number
  height: number
}

export function cornersOf(geometry: AreaShapeGeometry): PlanPoint[] {
  const radians = (geometry.rotation * Math.PI) / 180
  const cosine = Math.cos(radians)
  const sine = Math.sin(radians)
  const corners: PlanPoint[] = [
    { x: 0, y: 0 },
    { x: geometry.width, y: 0 },
    { x: geometry.width, y: geometry.height },
    { x: 0, y: geometry.height },
  ]
  return corners.map((corner) => ({
    x: geometry.x + corner.x * cosine - corner.y * sine,
    y: geometry.y + corner.x * sine + corner.y * cosine,
  }))
}

export function lowestEdgeOf(geometries: readonly AreaShapeGeometry[]): number {
  return Math.max(0, ...geometries.flatMap((geometry) => cornersOf(geometry).map((corner) => corner.y)))
}

export function scaleFittingTheWholeDrawing(view: PlanSize, drawing: PlanSize): number {
  return Math.min(view.width / drawing.width, view.height / drawing.height)
}
