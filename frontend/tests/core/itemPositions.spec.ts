import { describe, expect, it } from 'vitest'
import { groupPositions, portionsOfItem, positionsForItem } from '../../src/core/itemPositions'
import type { ItemPosition } from '../../src/core/itemPositions'
import type { CatalogItem, DraftLine, DraftOrder } from '../../src/core/apiTypes'

function item(stationIds: string[]): CatalogItem {
  return {
    id: 'item-1',
    name: 'Bier',
    categoryId: 'category-getraenke',
    priceCents: 400,
    sortOrder: 0,
    isAvailable: true,
    stationIds,
  }
}

function line(catalogItemId: string, note: string | null, stationId: string | null): DraftLine {
  return { catalogItemId, note, stationId, name: 'Bier' }
}

function draftWith(lines: DraftLine[]): DraftOrder {
  return { tableName: '', note: null, lines, clientOrderId: null }
}

const stationNameOf = (stationId: string): string =>
  stationId === 'station-1' ? 'Bar innen' : 'Bar aussen'

describe('positionsForItem', () => {
  it('returns one entry per position of that item, in the order they were chosen', () => {
    const draft = draftWith([
      line('item-1', null, null),
      line('item-2', null, null),
      line('item-1', 'ohne Schaum', null),
    ])

    const positions = positionsForItem(draft, item(['station-1']), stationNameOf)

    expect(positions.map((position) => position.index)).toEqual([0, 2])
    expect(positions[1].note).toBe('ohne Schaum')
  })

  it('reports no station choice for an item only one station prepares', () => {
    const draft = draftWith([line('item-1', null, 'station-1')])

    const positions = positionsForItem(draft, item(['station-1']), stationNameOf)

    expect(positions[0].hasAStationChoice).toBe(false)
    expect(positions[0].stationName).toBeNull()
  })

  it('names the chosen station on an item two stations could prepare', () => {
    const draft = draftWith([line('item-1', null, 'station-2')])

    const positions = positionsForItem(draft, item(['station-1', 'station-2']), stationNameOf)

    expect(positions[0].hasAStationChoice).toBe(true)
    expect(positions[0].stationName).toBe('Bar aussen')
  })

  it('leaves the station unnamed while the server has not chosen one yet', () => {
    const draft = draftWith([line('item-1', null, null)])

    const positions = positionsForItem(draft, item(['station-1', 'station-2']), stationNameOf)

    expect(positions[0].stationName).toBeNull()
  })

  it('returns nothing for an item that is not in the basket', () => {
    const draft = draftWith([line('item-2', null, null)])

    expect(positionsForItem(draft, item(['station-1']), stationNameOf)).toEqual([])
  })
})

describe('portionsOfItem', () => {
  it('counts every position of that item, whether or not it carries a note', () => {
    const draft = draftWith([
      line('item-1', null, null),
      line('item-2', null, null),
      line('item-1', 'ohne Schaum', null),
    ])

    expect(portionsOfItem(draft, 'item-1')).toBe(2)
  })

  it('counts nothing for an item that is not on the order', () => {
    const draft = draftWith([line('item-2', null, null)])

    expect(portionsOfItem(draft, 'item-1')).toBe(0)
  })
})

describe('groupPositions', () => {
  function position(index: number, note: string | null, stationName: string | null): ItemPosition {
    return { index, note, hasAStationChoice: stationName !== null, stationName }
  }

  it('counts positions without a note or a station of their own as one group', () => {
    const groups = groupPositions([
      position(0, null, null),
      position(1, null, null),
      position(2, null, null),
    ])

    expect(groups).toHaveLength(1)
    expect(groups[0].note).toBeNull()
    expect(groups[0].stationName).toBeNull()
    expect(groups[0].indexes).toEqual([0, 1, 2])
  })

  it('gives a position carrying a note a group of its own', () => {
    const groups = groupPositions([
      position(0, null, null),
      position(1, 'ohne Eis', null),
      position(2, null, null),
    ])

    expect(groups).toHaveLength(2)
    expect(groups[0].indexes).toEqual([0, 2])
    expect(groups[1].note).toBe('ohne Eis')
    expect(groups[1].indexes).toEqual([1])
  })

  it('counts positions carrying the same note together', () => {
    const groups = groupPositions([position(0, 'ohne Eis', null), position(1, 'ohne Eis', null)])

    expect(groups).toHaveLength(1)
    expect(groups[0].indexes).toEqual([0, 1])
  })

  it('keeps positions apart when they go to different stations', () => {
    const groups = groupPositions([
      position(0, null, 'Bar innen'),
      position(1, null, 'Bar aussen'),
      position(2, null, 'Bar innen'),
    ])

    expect(groups).toHaveLength(2)
    expect(groups[0].stationName).toBe('Bar innen')
    expect(groups[0].indexes).toEqual([0, 2])
    expect(groups[1].indexes).toEqual([1])
  })

  it('keeps the order the groups first appeared in', () => {
    const groups = groupPositions([
      position(0, 'ohne Eis', null),
      position(1, null, null),
      position(2, 'ohne Eis', null),
    ])

    expect(groups.map((entry) => entry.note)).toEqual(['ohne Eis', null])
  })

  it('returns nothing for an item that is not on the order', () => {
    expect(groupPositions([])).toEqual([])
  })
})
