import { describe, expect, it } from 'vitest'
import {
  isDrawnIn,
  isTableSelected,
  nodeIdOfStation,
  nodeIdOfTable,
  selectedTableIds,
  selectionOfTables,
  shapeNodeIdOf,
  tableIdsInMarquee,
  withTableToggled,
} from '../../../src/admin/core/tablePlanSelection'
import { aGeometry, anAdminArea, anAdminStationShape, anAdminTable } from '../../support/wireViews'

const A1 = anAdminTable({ tableId: 'table-a1', geometry: aGeometry({ x: 20, y: 30, width: 80, height: 40 }) })

const A2 = anAdminTable({ tableId: 'table-a2', geometry: aGeometry({ x: 150, y: 30, width: 80, height: 40 }) })

const HALL = anAdminArea({
  tables: [A1, A2],
  stationShapes: [anAdminStationShape({ stationId: 'station-schank' })],
})

describe('the id a shape carries in the drawing', () => {
  it('names a table by its table id', () => {
    expect([nodeIdOfTable('table-a1'), shapeNodeIdOf({ kind: 'table', tableId: 'table-a1' })]).toEqual([
      'table-table-a1',
      'table-table-a1',
    ])
  })

  it('names a station by its station id', () => {
    expect([nodeIdOfStation('station-schank'), shapeNodeIdOf({ kind: 'station', stationId: 'station-schank' })]).toEqual([
      'station-station-schank',
      'station-station-schank',
    ])
  })
})

describe('a table selection', () => {
  it('holds every chosen table once', () => {
    expect(selectionOfTables(['table-a1', 'table-a2', 'table-a1'])).toEqual({
      kind: 'tables',
      tableIds: ['table-a1', 'table-a2'],
    })
  })

  it('adds a table that is not selected yet', () => {
    const added = withTableToggled(selectionOfTables(['table-a1']), 'table-a2')

    expect(selectedTableIds(added)).toEqual(['table-a1', 'table-a2'])
  })

  it('takes a selected table out again', () => {
    const removed = withTableToggled(selectionOfTables(['table-a1', 'table-a2']), 'table-a1')

    expect(selectedTableIds(removed)).toEqual(['table-a2'])
  })

  it('is empty once its last table is taken out', () => {
    expect(withTableToggled(selectionOfTables(['table-a1']), 'table-a1')).toBeNull()
  })

  it('replaces a selected station when a table joins', () => {
    const toggled = withTableToggled({ kind: 'station', stationId: 'station-schank' }, 'table-a1')

    expect(toggled).toEqual({ kind: 'tables', tableIds: ['table-a1'] })
  })
})

describe('a selected shape', () => {
  it('is drawn in the area that holds the table', () => {
    expect(isDrawnIn(HALL, { kind: 'tables', tableIds: ['table-a1'] })).toBe(true)
  })

  it('is drawn in the area that holds the station', () => {
    expect(isDrawnIn(HALL, { kind: 'station', stationId: 'station-schank' })).toBe(true)
  })

  it('is not drawn in an area without that table', () => {
    expect(isDrawnIn(HALL, { kind: 'tables', tableIds: ['table-b1'] })).toBe(false)
  })

  it('is not drawn once one of its tables is hidden', () => {
    const hallWithAHiddenTable = anAdminArea({
      tables: [A1, anAdminTable({ tableId: 'table-a2', isHidden: true })],
    })

    expect(isDrawnIn(hallWithAHiddenTable, { kind: 'tables', tableIds: ['table-a1', 'table-a2'] })).toBe(false)
  })
})

describe('the tables a marquee covers', () => {
  it('is every table whose shape the box touches', () => {
    expect(tableIdsInMarquee(HALL, { x: 10, y: 20, width: 100, height: 100 })).toEqual(['table-a1'])
  })

  it('is both tables when the box spans the row', () => {
    expect(tableIdsInMarquee(HALL, { x: 0, y: 20, width: 300, height: 100 })).toEqual(['table-a1', 'table-a2'])
  })

  it('is no table when the box lies beside them', () => {
    expect(tableIdsInMarquee(HALL, { x: 0, y: 200, width: 300, height: 50 })).toEqual([])
  })

  it('leaves out a hidden table inside the box', () => {
    const hallWithAHiddenTable = anAdminArea({
      tables: [A1, anAdminTable({ tableId: 'table-a2', isHidden: true, geometry: aGeometry({ x: 150, y: 30 }) })],
    })

    expect(tableIdsInMarquee(hallWithAHiddenTable, { x: 0, y: 20, width: 300, height: 100 })).toEqual(['table-a1'])
  })

  it('follows the corners of a turned table', () => {
    const turned = anAdminTable({ tableId: 'table-a1', geometry: aGeometry({ x: 100, y: 100, width: 80, height: 40, rotation: 90 }) })

    expect(tableIdsInMarquee(anAdminArea({ tables: [turned] }), { x: 80, y: 120, width: 40, height: 40 })).toEqual([
      'table-a1',
    ])
  })
})

describe('which table a selection names', () => {
  it('says a table is selected only while it is in the selection', () => {
    expect([
      isTableSelected(selectionOfTables(['table-a1']), 'table-a1'),
      isTableSelected(selectionOfTables(['table-a1']), 'table-a2'),
      isTableSelected({ kind: 'station', stationId: 'station-schank' }, 'table-a1'),
    ]).toEqual([true, false, false])
  })
})
