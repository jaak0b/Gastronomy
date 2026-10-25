import { describe, expect, it } from 'vitest'
import {
  NEW_STATION_SIZE,
  NEW_TABLE_SIZE,
  drawnTablesOf,
  editorDrawingLength,
  fitsTheDrawing,
  geometryForANewShapeIn,
  pastedCopiesAt,
  pastedCopiesBeside,
} from '../../../src/admin/core/tablePlanPlacement'
import { aGeometry, anAdminArea, anAdminStationShape, anAdminTable } from '../../support/wireViews'

const SHOWN_TABLE = anAdminTable({ tableId: 'table-a1', geometry: aGeometry({ y: 30 }) })

const HIDDEN_TABLE = anAdminTable({ tableId: 'table-a2', isHidden: true, geometry: aGeometry({ y: 300 }) })

const STATION_SHAPE = anAdminStationShape({ geometry: aGeometry({ x: 200, y: 80 }) })

const HALL = anAdminArea({ tables: [SHOWN_TABLE, HIDDEN_TABLE], stationShapes: [STATION_SHAPE] })

describe('what the editor draws of an area', () => {
  it('is every shown table, never a hidden one', () => {
    expect(drawnTablesOf(HALL)).toEqual([SHOWN_TABLE])
  })
})

describe('where a new table or station is put', () => {
  it('is near the top left corner of an empty area', () => {
    expect(geometryForANewShapeIn(anAdminArea(), NEW_TABLE_SIZE)).toEqual({
      x: 10,
      y: 10,
      width: 80,
      height: 40,
      rotation: 0,
    })
  })

  it('is below the lowest table or station shape, so it never covers one', () => {
    const area = anAdminArea({
      tables: [anAdminTable({ geometry: aGeometry({ y: 30, height: 40 }) })],
      stationShapes: [anAdminStationShape({ geometry: aGeometry({ y: 100, height: 50 }) })],
    })

    expect(geometryForANewShapeIn(area, NEW_STATION_SIZE)).toEqual({
      x: 10,
      y: 160,
      width: 120,
      height: 40,
      rotation: 0,
    })
  })

  it('is below a hidden table as well, so showing that table again never stacks two shapes', () => {
    expect(geometryForANewShapeIn(HALL, NEW_STATION_SIZE).y).toBe(350)
  })

  it('is below the lowest corner of a turned shape', () => {
    const turned = anAdminTable({ geometry: aGeometry({ x: 100, y: 100, width: 80, height: 40, rotation: 90 }) })

    expect(geometryForANewShapeIn(anAdminArea({ tables: [turned] }), NEW_TABLE_SIZE).y).toBeCloseTo(190)
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

describe('where a copied table is pasted', () => {
  const COPIES = [
    { sourceTableId: 'table-a1', geometry: aGeometry({ x: 20, y: 30, width: 80, height: 40, rotation: 15 }) },
    { sourceTableId: 'table-a2', geometry: aGeometry({ x: 150, y: 60, width: 80, height: 40 }) },
  ]

  it('moves one grid step right and down when it stays in the same area', () => {
    expect(pastedCopiesBeside(COPIES, 'area-halle', 'area-halle')).toEqual([
      { sourceTableId: 'table-a1', geometry: { x: 30, y: 40, width: 80, height: 40, rotation: 15 } },
      { sourceTableId: 'table-a2', geometry: { x: 160, y: 70, width: 80, height: 40, rotation: 0 } },
    ])
  })

  it('keeps the coordinates the originals have when it goes to another area', () => {
    expect(pastedCopiesBeside(COPIES, 'area-halle', 'area-zelt')).toEqual(COPIES)
  })

  it('shifts the whole group left when it would cross the right edge', () => {
    const nearTheEdge = [
      { sourceTableId: 'table-a1', geometry: aGeometry({ x: 340, y: 30, width: 80, height: 40 }) },
      { sourceTableId: 'table-a2', geometry: aGeometry({ x: 250, y: 90, width: 80, height: 40 }) },
    ]

    expect(pastedCopiesBeside(nearTheEdge, 'area-halle', 'area-zelt').map((copy) => copy.geometry)).toEqual([
      { x: 320, y: 30, width: 80, height: 40, rotation: 0 },
      { x: 230, y: 90, width: 80, height: 40, rotation: 0 },
    ])
  })

  it('never shifts the left edge below zero', () => {
    const widerThanTheDrawing = [
      { sourceTableId: 'table-a1', geometry: aGeometry({ x: 0, y: 30, width: 300, height: 40 }) },
      { sourceTableId: 'table-a2', geometry: aGeometry({ x: 250, y: 90, width: 300, height: 40 }) },
    ]

    expect(pastedCopiesBeside(widerThanTheDrawing, 'area-halle', 'area-zelt').map((copy) => copy.geometry.x)).toEqual([
      0,
      250,
    ])
  })

  const FLAT_COPIES = [
    { sourceTableId: 'table-a1', geometry: aGeometry({ x: 20, y: 30, width: 80, height: 40 }) },
    { sourceTableId: 'table-a2', geometry: aGeometry({ x: 150, y: 60, width: 80, height: 40 }) },
  ]

  it('puts the top left of the group at the clicked point', () => {
    expect(pastedCopiesAt(FLAT_COPIES, { x: 100, y: 200 })).toEqual([
      { sourceTableId: 'table-a1', geometry: { x: 100, y: 200, width: 80, height: 40, rotation: 0 } },
      { sourceTableId: 'table-a2', geometry: { x: 230, y: 230, width: 80, height: 40, rotation: 0 } },
    ])
  })

  it('keeps the group inside the drawing width when it is put near the right edge', () => {
    expect(pastedCopiesAt(FLAT_COPIES, { x: 390, y: 200 }).map((copy) => copy.geometry.x)).toEqual([190, 320])
  })
})
