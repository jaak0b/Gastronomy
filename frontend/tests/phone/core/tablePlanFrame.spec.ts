import { describe, expect, it } from 'vitest'
import { rectangleToStrokeInside } from '../../../src/phone/core/tablePlanFrame'

describe('the rectangle to stroke so that a table\'s frame stays inside its size', () => {
  it('lies half the stroke width in from every side of the size', () => {
    expect(rectangleToStrokeInside({ width: 80, height: 40 }, 4)).toEqual({ x: 2, y: 2, width: 76, height: 36 })
  })

  it('lies less far in for a thinner stroke', () => {
    expect(rectangleToStrokeInside({ width: 60, height: 30 }, 2)).toEqual({ x: 1, y: 1, width: 58, height: 28 })
  })
})
