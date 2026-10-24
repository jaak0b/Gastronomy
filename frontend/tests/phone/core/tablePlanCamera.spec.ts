import { describe, expect, it } from 'vitest'
import {
  cameraFittingTheWidth,
  clampedPosition,
  pinchedCamera,
  pinchOf,
  waiterDrawingLength,
} from '../../../src/phone/core/tablePlanCamera'
import { aGeometry } from '../../support/wireViews'

const PHONE = { width: 360, height: 640 }

describe('the camera a plan opens with', () => {
  it('fits the drawing to the width of the screen and starts at its top left corner', () => {
    expect(cameraFittingTheWidth(PHONE)).toEqual({ scale: 0.9, x: 0, y: 0 })
  })
})

describe('how long the waiter\'s drawing is', () => {
  it('reaches a little below the lowest shape', () => {
    expect(waiterDrawingLength([aGeometry({ y: 300, height: 40 }), aGeometry({ y: 100, height: 40 })])).toBe(360)
  })
})

describe('panning the plan', () => {
  it('keeps the left edge of the drawing at the left edge of the screen', () => {
    expect(clampedPosition({ x: 50, y: 0 }, 1.8, PHONE, 800)).toEqual({ x: 0, y: 0 })
  })

  it('stops once the right edge of the drawing reaches the right edge of the screen', () => {
    expect(clampedPosition({ x: -500, y: 0 }, 1.8, PHONE, 800)).toEqual({ x: -360, y: 0 })
  })

  it('stops once the bottom of the drawing reaches the bottom of the screen', () => {
    expect(clampedPosition({ x: 0, y: -1000 }, 1.8, PHONE, 800)).toEqual({ x: 0, y: -800 })
  })

  it('keeps a drawing shorter than the screen at the top', () => {
    expect(clampedPosition({ x: 0, y: -100 }, 0.9, PHONE, 300)).toEqual({ x: 0, y: 0 })
  })

  it('moves freely between those edges', () => {
    expect(clampedPosition({ x: -120, y: -200 }, 1.8, PHONE, 800)).toEqual({ x: -120, y: -200 })
  })
})

describe('pinching the plan', () => {
  const fitted = { scale: 0.9, x: 0, y: 0 }

  it('measures the middle and the distance between two fingers', () => {
    expect(pinchOf({ x: 100, y: 200 }, { x: 160, y: 280 })).toEqual({ centre: { x: 130, y: 240 }, distance: 100 })
  })

  it('zooms in by the factor the fingers spread, around the point between them', () => {
    const from = { centre: { x: 180, y: 300 }, distance: 100 }
    const to = { centre: { x: 180, y: 300 }, distance: 200 }

    expect(pinchedCamera(fitted, from, to, PHONE, 800)).toEqual({ scale: 1.8, x: -180, y: -300 })
  })

  it('zooms no further out than the width of the screen', () => {
    const from = { centre: { x: 180, y: 300 }, distance: 200 }
    const to = { centre: { x: 180, y: 300 }, distance: 100 }

    expect(pinchedCamera(fitted, from, to, PHONE, 800)).toEqual({ scale: 0.9, x: 0, y: 0 })
  })

  it('zooms no further in than four times the width of the screen', () => {
    const from = { centre: { x: 0, y: 0 }, distance: 100 }
    const to = { centre: { x: 0, y: 0 }, distance: 1000 }

    expect(pinchedCamera(fitted, from, to, PHONE, 800).scale).toBe(3.6)
  })

  it('pans with the fingers when they move without spreading', () => {
    const zoomed = { scale: 1.8, x: -180, y: -300 }
    const from = { centre: { x: 180, y: 300 }, distance: 100 }
    const to = { centre: { x: 150, y: 260 }, distance: 100 }

    expect(pinchedCamera(zoomed, from, to, PHONE, 800)).toEqual({ scale: 1.8, x: -210, y: -340 })
  })
})
