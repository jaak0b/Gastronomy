import { describe, expect, it } from 'vitest'
import { isDrawnIn, shapeNodeIdOf } from '../../../src/admin/core/tablePlanSelection'
import { anAdminArea, anAdminStationShape, anAdminTable } from '../../support/wireViews'

const HALL = anAdminArea({
  tables: [anAdminTable({ tableId: 'table-a1' })],
  stationShapes: [anAdminStationShape({ stationId: 'station-schank' })],
})

describe('the id a shape carries in the drawing', () => {
  it('names a table by its table id', () => {
    expect(shapeNodeIdOf({ kind: 'table', tableId: 'table-a1' })).toBe('table-table-a1')
  })

  it('names a station by its station id', () => {
    expect(shapeNodeIdOf({ kind: 'station', stationId: 'station-schank' })).toBe('station-station-schank')
  })
})

describe('a selected shape', () => {
  it('is drawn in the area that holds the table', () => {
    expect(isDrawnIn(HALL, { kind: 'table', tableId: 'table-a1' })).toBe(true)
  })

  it('is drawn in the area that holds the station', () => {
    expect(isDrawnIn(HALL, { kind: 'station', stationId: 'station-schank' })).toBe(true)
  })

  it('is not drawn in an area without that table', () => {
    expect(isDrawnIn(HALL, { kind: 'table', tableId: 'table-b1' })).toBe(false)
  })
})
