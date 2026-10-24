import { describe, expect, it } from 'vitest'
import {
  NEW_STATION_SIZE,
  NEW_TABLE_SIZE,
  editorDrawingLength,
  fitsTheDrawing,
  geometryBelowTheLowestShape,
} from '../../../src/admin/core/tablePlanPlacement'
import { aGeometry } from '../../support/wireViews'

describe('where a new table or station is put', () => {
  it('is near the top left corner of an empty area', () => {
    expect(geometryBelowTheLowestShape([], NEW_TABLE_SIZE)).toEqual({
      x: 10,
      y: 10,
      width: 80,
      height: 40,
      rotation: 0,
    })
  })

  it('is below the lowest shape, so it never covers one', () => {
    const shapes = [aGeometry({ y: 30, height: 40 }), aGeometry({ y: 100, height: 50 })]

    expect(geometryBelowTheLowestShape(shapes, NEW_STATION_SIZE)).toEqual({
      x: 10,
      y: 160,
      width: 120,
      height: 40,
      rotation: 0,
    })
  })

  it('is below the lowest corner of a turned shape', () => {
    const turned = aGeometry({ x: 100, y: 100, width: 80, height: 40, rotation: 90 })

    expect(geometryBelowTheLowestShape([turned], NEW_TABLE_SIZE).y).toBeCloseTo(190)
  })
})

describe('a size the admin drags a shape to', () => {
  it('fits when the shape stays inside the drawing', () => {
    expect(fitsTheDrawing(aGeometry({ x: 0, y: 0, width: 400, height: 20 }))).toBe(true)
  })

  it('does not fit when the shape gets narrower than its name needs', () => {
    expect(fitsTheDrawing(aGeometry({ width: 19 }))).toBe(false)
  })

  it('does not fit when the shape gets lower than its name needs', () => {
    expect(fitsTheDrawing(aGeometry({ height: 19 }))).toBe(false)
  })

  it('does not fit when the shape reaches past the right edge', () => {
    expect(fitsTheDrawing(aGeometry({ x: 330, width: 80 }))).toBe(false)
  })

  it('does not fit when the shape reaches past the left edge', () => {
    expect(fitsTheDrawing(aGeometry({ x: -1 }))).toBe(false)
  })

  it('does not fit when the shape reaches above the top', () => {
    expect(fitsTheDrawing(aGeometry({ y: -1 }))).toBe(false)
  })

  it('does not fit when a corner of a turned shape reaches past the left edge', () => {
    expect(fitsTheDrawing(aGeometry({ x: 20, y: 100, height: 40, rotation: 90 }))).toBe(false)
  })
})

describe('the length of the drawing in the editor', () => {
  it('leaves room below the lowest shape to drag into', () => {
    expect(editorDrawingLength([aGeometry({ y: 30, height: 40 })], 0)).toBe(270)
  })

  it('fills the visible part of the screen when the shapes end higher up', () => {
    expect(editorDrawingLength([aGeometry({ y: 30, height: 40 })], 500)).toBe(500)
  })
})
