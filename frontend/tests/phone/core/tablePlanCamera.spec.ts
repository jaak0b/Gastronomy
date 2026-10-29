import { describe, expect, it } from 'vitest'
import {
  clampedPosition,
  pinchedCamera,
  pinchOf,
  startingCamera,
  waiterDrawingLength,
} from '../../../src/phone/core/tablePlanCamera'
import { aGeometry } from '../../support/wireViews'

const PHONE = { width: 360, height: 640 }

const DESKTOP = { width: 1900, height: 940 }

const NARROWEST_SCREEN_SHOWING_THE_WHOLE_PLAN = 960

describe('the camera a plan opens with', () => {
  it('fits the drawing to the width of the screen and starts at its top left corner', () => {
    expect(startingCamera(PHONE, 800, NARROWEST_SCREEN_SHOWING_THE_WHOLE_PLAN)).toEqual({ scale: 0.9, x: 0, y: 0 })
  })

  it('shows the whole plan in the middle of a screen wider than a phone', () => {
    expect(startingCamera(DESKTOP, 470, NARROWEST_SCREEN_SHOWING_THE_WHOLE_PLAN)).toEqual({ scale: 2, x: 550, y: 0 })
  })

  it('shows the whole plan from the width at which a screen stops being a phone', () => {
    expect(startingCamera({ width: 960, height: 470 }, 470, NARROWEST_SCREEN_SHOWING_THE_WHOLE_PLAN)).toEqual({ scale: 1, x: 280, y: 0 })
  })

  it('fits a plan that is short for a wide screen to the width of the screen', () => {
    expect(startingCamera(DESKTOP, 100, NARROWEST_SCREEN_SHOWING_THE_WHOLE_PLAN)).toEqual({ scale: 4.75, x: 0, y: 0 })
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

  it('keeps a drawing narrower than the screen in the middle of it', () => {
    expect(clampedPosition({ x: 30, y: 0 }, 2, DESKTOP, 470)).toEqual({ x: 550, y: 0 })
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

    expect(pinchedCamera(fitted, from, to, PHONE, 800, NARROWEST_SCREEN_SHOWING_THE_WHOLE_PLAN)).toEqual({ scale: 1.8, x: -180, y: -300 })
  })

  it('zooms no further out than the width of the screen', () => {
    const from = { centre: { x: 180, y: 300 }, distance: 200 }
    const to = { centre: { x: 180, y: 300 }, distance: 100 }

    expect(pinchedCamera(fitted, from, to, PHONE, 800, NARROWEST_SCREEN_SHOWING_THE_WHOLE_PLAN)).toEqual({ scale: 0.9, x: 0, y: 0 })
  })

  it('zooms no further in than four times the width of the screen', () => {
    const from = { centre: { x: 0, y: 0 }, distance: 100 }
    const to = { centre: { x: 0, y: 0 }, distance: 1000 }

    expect(pinchedCamera(fitted, from, to, PHONE, 800, NARROWEST_SCREEN_SHOWING_THE_WHOLE_PLAN).scale).toBe(3.6)
  })

  it('pans with the fingers when they move without spreading', () => {
    const zoomed = { scale: 1.8, x: -180, y: -300 }
    const from = { centre: { x: 180, y: 300 }, distance: 100 }
    const to = { centre: { x: 150, y: 260 }, distance: 100 }

    expect(pinchedCamera(zoomed, from, to, PHONE, 800, NARROWEST_SCREEN_SHOWING_THE_WHOLE_PLAN)).toEqual({ scale: 1.8, x: -210, y: -340 })
  })
})

describe('pinching the plan on a screen wider than a phone', () => {
  const wholePlan = { scale: 2, x: 550, y: 0 }

  it('zooms no further out than the whole plan', () => {
    const from = { centre: { x: 950, y: 470 }, distance: 200 }
    const to = { centre: { x: 950, y: 470 }, distance: 100 }

    expect(pinchedCamera(wholePlan, from, to, DESKTOP, 470, NARROWEST_SCREEN_SHOWING_THE_WHOLE_PLAN)).toEqual({ scale: 2, x: 550, y: 0 })
  })

  it('zooms no further in than four times the whole plan', () => {
    const from = { centre: { x: 950, y: 470 }, distance: 100 }
    const to = { centre: { x: 950, y: 470 }, distance: 1000 }

    expect(pinchedCamera(wholePlan, from, to, DESKTOP, 470, NARROWEST_SCREEN_SHOWING_THE_WHOLE_PLAN).scale).toBe(8)
  })
})
