import { TABLE_PLAN_DRAWING_WIDTH, type PlanPoint, type PlanRectangle } from '../../shared/core/tablePlanDrawing'

export const SNAP_GRID = 10

const SNAP_DISTANCE = 6

type GuideOrientation = 'vertical' | 'horizontal'

export interface SnapGuide {
  orientation: GuideOrientation
  position: number
}

export interface SnapStops {
  vertical: readonly number[]
  horizontal: readonly number[]
}

interface SnappedPosition {
  x: number
  y: number
  guides: SnapGuide[]
}

interface SpanSnap {
  start: number
  guide: number | null
}

export function snapStopsBeside(otherBoxes: readonly PlanRectangle[]): SnapStops {
  return {
    vertical: [
      0,
      TABLE_PLAN_DRAWING_WIDTH / 2,
      TABLE_PLAN_DRAWING_WIDTH,
      ...otherBoxes.flatMap((box) => [box.x, box.x + box.width / 2, box.x + box.width]),
    ],
    horizontal: [0, ...otherBoxes.flatMap((box) => [box.y, box.y + box.height / 2, box.y + box.height])],
  }
}

function snapSpan(start: number, size: number, stops: readonly number[]): SpanSnap {
  let nearest: (SpanSnap & { distance: number }) | null = null
  for (const stop of stops) {
    for (const reach of [0, size / 2, size]) {
      const distance = Math.abs(stop - (start + reach))
      if (distance < SNAP_DISTANCE && (nearest === null || distance < nearest.distance)) {
        nearest = { start: stop - reach, guide: stop, distance }
      }
    }
  }
  if (nearest === null) {
    return { start: Math.round(start / SNAP_GRID) * SNAP_GRID, guide: null }
  }
  return { start: nearest.start, guide: nearest.guide }
}

export function snapDraggedBox(box: PlanRectangle, stops: SnapStops): SnappedPosition {
  const across = snapSpan(box.x, box.width, stops.vertical)
  const down = snapSpan(box.y, box.height, stops.horizontal)
  const x = Math.min(Math.max(across.start, 0), Math.max(0, TABLE_PLAN_DRAWING_WIDTH - box.width))
  const y = Math.max(down.start, 0)
  const guides: SnapGuide[] = []
  if (across.guide !== null && x === across.start) {
    guides.push({ orientation: 'vertical', position: across.guide })
  }
  if (down.guide !== null && y === down.start) {
    guides.push({ orientation: 'horizontal', position: down.guide })
  }
  return { x, y, guides }
}

export function snapResizeCorner(corner: PlanPoint, stops: SnapStops): SnappedPosition {
  const across = snapSpan(corner.x, 0, stops.vertical)
  const down = snapSpan(corner.y, 0, stops.horizontal)
  const guides: SnapGuide[] = []
  if (across.guide !== null) {
    guides.push({ orientation: 'vertical', position: across.guide })
  }
  if (down.guide !== null) {
    guides.push({ orientation: 'horizontal', position: down.guide })
  }
  return { x: across.start, y: down.start, guides }
}
