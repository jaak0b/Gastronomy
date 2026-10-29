import type { PlanRectangle, PlanSize } from '../../shared/core/tablePlanDrawing'

export function rectangleToStrokeInside(size: PlanSize, strokeWidth: number): PlanRectangle {
  const halfOfTheStroke = strokeWidth / 2
  return {
    x: halfOfTheStroke,
    y: halfOfTheStroke,
    width: size.width - strokeWidth,
    height: size.height - strokeWidth,
  }
}
