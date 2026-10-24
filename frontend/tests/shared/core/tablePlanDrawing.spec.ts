import { describe, expect, it } from 'vitest'
import { cornersOf, lowestEdgeOf } from '../../../src/shared/core/tablePlanDrawing'
import { aGeometry } from '../../support/wireViews'

describe('the corners of a shape in the plan', () => {
  it('are the corners of the rectangle for an upright shape', () => {
    expect(cornersOf(aGeometry({ x: 20, y: 30, width: 80, height: 40 }))).toEqual([
      { x: 20, y: 30 },
      { x: 100, y: 30 },
      { x: 100, y: 70 },
      { x: 20, y: 70 },
    ])
  })

  it('turn clockwise about the top left corner', () => {
    const corners = cornersOf(aGeometry({ x: 20, y: 30, width: 80, height: 40, rotation: 90 }))

    expect(corners.map((corner) => [corner.x, corner.y].map((value) => Math.round(value)))).toEqual([
      [20, 30],
      [20, 110],
      [-20, 110],
      [-20, 30],
    ])
  })
})

describe('the lowest edge of an area', () => {
  it('is the top of the drawing when nothing is drawn', () => {
    expect(lowestEdgeOf([])).toBe(0)
  })

  it('is the bottom of the lowest shape', () => {
    expect(lowestEdgeOf([aGeometry({ y: 30, height: 40 }), aGeometry({ y: 200, height: 50 })])).toBe(250)
  })
})
