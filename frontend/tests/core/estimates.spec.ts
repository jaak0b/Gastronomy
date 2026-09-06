import { describe, expect, it } from 'vitest'
import {
  pickerEstimateMinutes,
  queuedMinutesAt,
  readyInMinutes,
  sliceEstimateMinutes,
} from '../../src/core/estimates'
import type { CatalogItem, StationEstimate } from '../../src/core/apiTypes'

const QUEUES: StationEstimate[] = [
  { stationId: 'station-kueche', queuedMinutes: 12 },
  { stationId: 'station-grill', queuedMinutes: 4 },
]

function item(stationIds: string[], productionMinutes: number | null): CatalogItem {
  return {
    id: 'item-1',
    name: 'Bratwurst',
    categoryName: 'Essen',
    priceCents: 350,
    sortOrder: 1,
    isAvailable: true,
    stationIds,
    productionMinutes,
  }
}

describe('readyInMinutes', () => {
  it('adds the queue at the station to the time the item itself takes', () => {
    expect(readyInMinutes(12, 8)).toBe(20)
  })

  it('counts an item without a preparation time as taking no time', () => {
    expect(readyInMinutes(12, null)).toBe(12)
  })

  it('reports zero when nothing is queued and the item takes no time', () => {
    expect(readyInMinutes(0, null)).toBe(0)
  })
})

describe('queuedMinutesAt', () => {
  it('finds the queue of the station', () => {
    expect(queuedMinutesAt(QUEUES, 'station-grill')).toBe(4)
  })

  it('counts a station the laptop said nothing about as having no queue', () => {
    expect(queuedMinutesAt(QUEUES, 'station-unknown')).toBe(0)
  })
})

describe('sliceEstimateMinutes', () => {
  it('takes the slowest item when the station hands everything out together', () => {
    expect(sliceEstimateMinutes([20, 12, 15], 'together')).toBe(20)
  })

  it('gives no slice estimate when items come out as they are ready', () => {
    expect(sliceEstimateMinutes([20, 12, 15], 'asItComes')).toBeNull()
  })

  it('gives no slice estimate for an empty slice', () => {
    expect(sliceEstimateMinutes([], 'together')).toBeNull()
  })
})

describe('pickerEstimateMinutes, what the item list shows before a station is chosen', () => {
  it('uses the one station an item can go to', () => {
    expect(pickerEstimateMinutes(item(['station-kueche'], 8), QUEUES)).toBe(20)
  })

  it('takes the quickest station when the item could go to several', () => {
    expect(pickerEstimateMinutes(item(['station-kueche', 'station-grill'], 8), QUEUES)).toBe(12)
  })

  it('shows nothing for an item that has no station at all', () => {
    expect(pickerEstimateMinutes(item([], 8), QUEUES)).toBeNull()
  })
})
