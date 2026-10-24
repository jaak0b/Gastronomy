import { describe, expect, it } from 'vitest'
import { scrollBarsOf } from '../../../src/phone/core/tablePlanScrollBars'

const PHONE = { width: 400, height: 600 }

describe('the scroll bars of a plan fitted to the width of the screen', () => {
  it('shows neither bar when the whole drawing fits on the screen', () => {
    expect(scrollBarsOf({ scale: 1, x: 0, y: 0 }, PHONE, 500)).toEqual({ vertical: null, horizontal: null })
  })

  it('shows only the vertical bar for an area longer than the screen, as long as the share on screen', () => {
    expect(scrollBarsOf({ scale: 1, x: 0, y: 0 }, PHONE, 1200)).toEqual({
      vertical: { offset: 0, length: 300 },
      horizontal: null,
    })
  })
})

describe('the scroll bars of a plan zoomed in', () => {
  it('shows the horizontal bar as well, as long as the share of the width on screen', () => {
    expect(scrollBarsOf({ scale: 2, x: 0, y: 0 }, PHONE, 500)).toEqual({
      vertical: { offset: 0, length: 360 },
      horizontal: { offset: 0, length: 200 },
    })
  })
})

describe('the scroll bars of a plan panned to its edges', () => {
  it('puts the vertical bar at the bottom once the bottom of the drawing is on screen', () => {
    expect(scrollBarsOf({ scale: 1, x: 0, y: -600 }, PHONE, 1200).vertical).toEqual({ offset: 300, length: 300 })
  })

  it('puts the vertical bar at the top while the top of the drawing is on screen', () => {
    expect(scrollBarsOf({ scale: 2, x: -400, y: 0 }, PHONE, 500).vertical).toEqual({ offset: 0, length: 360 })
  })

  it('puts the horizontal bar at the right once the right edge of the drawing is on screen', () => {
    expect(scrollBarsOf({ scale: 2, x: -400, y: 0 }, PHONE, 500).horizontal).toEqual({ offset: 200, length: 200 })
  })

  it('puts the horizontal bar at the left while the left edge of the drawing is on screen', () => {
    expect(scrollBarsOf({ scale: 2, x: 0, y: -400 }, PHONE, 500).horizontal).toEqual({ offset: 0, length: 200 })
  })
})
