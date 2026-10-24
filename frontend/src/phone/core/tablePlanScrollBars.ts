import { TABLE_PLAN_DRAWING_WIDTH } from '../../shared/core/tablePlanDrawing'
import type { PlanCamera, PlanViewport } from './tablePlanCamera'

const PIXELS_OUT_OF_VIEW_BEFORE_A_BAR_SHOWS = 1

interface ScrollBar {
  offset: number
  length: number
}

interface PlanScrollBars {
  vertical: ScrollBar | null
  horizontal: ScrollBar | null
}

function scrollBarAlong(screenLength: number, drawnLength: number, drawingPosition: number): ScrollBar | null {
  if (drawnLength - screenLength < PIXELS_OUT_OF_VIEW_BEFORE_A_BAR_SHOWS) {
    return null
  }
  const scrolledPast = Math.max(0, -drawingPosition)
  return {
    offset: (scrolledPast * screenLength) / drawnLength,
    length: (screenLength * screenLength) / drawnLength,
  }
}

export function scrollBarsOf(camera: PlanCamera, viewport: PlanViewport, drawingLength: number): PlanScrollBars {
  return {
    vertical: scrollBarAlong(viewport.height, drawingLength * camera.scale, camera.y),
    horizontal: scrollBarAlong(viewport.width, TABLE_PLAN_DRAWING_WIDTH * camera.scale, camera.x),
  }
}
