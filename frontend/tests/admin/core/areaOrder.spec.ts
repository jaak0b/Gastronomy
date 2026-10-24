import { describe, expect, it } from 'vitest'
import { sortOrderChangesForMoving, sortOrderForANewArea } from '../../../src/admin/core/areaOrder'
import { anAdminArea } from '../../support/wireViews'

const HALL = anAdminArea({ areaId: 'area-halle', name: 'Halle', sortOrder: 1 })

const TENT = anAdminArea({ areaId: 'area-zelt', name: 'Zelt', sortOrder: 2 })

const GARDEN = anAdminArea({ areaId: 'area-garten', name: 'Garten', sortOrder: 3 })

describe('moving an area in the order of the phone', () => {
  it('swaps it with the area above when it moves up', () => {
    expect(sortOrderChangesForMoving([HALL, TENT, GARDEN], 'area-garten', 'up')).toEqual([
      { area: GARDEN, sortOrder: 2 },
      { area: TENT, sortOrder: 3 },
    ])
  })

  it('swaps it with the area below when it moves down', () => {
    expect(sortOrderChangesForMoving([HALL, TENT, GARDEN], 'area-halle', 'down')).toEqual([
      { area: TENT, sortOrder: 1 },
      { area: HALL, sortOrder: 2 },
    ])
  })

  it('numbers every area from one when they shared a place in the order', () => {
    const hall = anAdminArea({ areaId: 'area-halle', name: 'Halle', sortOrder: 0 })
    const tent = anAdminArea({ areaId: 'area-zelt', name: 'Zelt', sortOrder: 0 })

    expect(sortOrderChangesForMoving([hall, tent], 'area-zelt', 'up')).toEqual([
      { area: tent, sortOrder: 1 },
      { area: hall, sortOrder: 2 },
    ])
  })

  it('changes nothing when the first area moves up', () => {
    expect(sortOrderChangesForMoving([HALL, TENT], 'area-halle', 'up')).toEqual([])
  })

  it('changes nothing when the last area moves down', () => {
    expect(sortOrderChangesForMoving([HALL, TENT], 'area-zelt', 'down')).toEqual([])
  })
})

describe('the place of a new area in the order', () => {
  it('is the first one when the festival has no area yet', () => {
    expect(sortOrderForANewArea([])).toBe(1)
  })

  it('comes after the last area', () => {
    expect(sortOrderForANewArea([HALL, GARDEN, TENT])).toBe(4)
  })
})
