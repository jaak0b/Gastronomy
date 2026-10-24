import { describe, expect, it } from 'vitest'
import { sortOrderForANewArea } from '../../../src/admin/core/areaOrder'
import { anAdminArea } from '../../support/wireViews'

const HALL = anAdminArea({ areaId: 'area-halle', name: 'Halle', sortOrder: 1 })

const TENT = anAdminArea({ areaId: 'area-zelt', name: 'Zelt', sortOrder: 2 })

const GARDEN = anAdminArea({ areaId: 'area-garten', name: 'Garten', sortOrder: 3 })

describe('the place of a new area in the order', () => {
  it('is the first one when the festival has no area yet', () => {
    expect(sortOrderForANewArea([])).toBe(1)
  })

  it('comes after the last area', () => {
    expect(sortOrderForANewArea([HALL, GARDEN, TENT])).toBe(4)
  })
})
