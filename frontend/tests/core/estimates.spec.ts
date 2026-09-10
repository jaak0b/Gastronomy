import { describe, expect, it } from 'vitest'
import {
  lineEstimateMinutes,
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
    categoryId: 'category-essen',
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

  it('reports zero when nothing is queued and the item takes no time', () => {
    expect(readyInMinutes(0, 0)).toBe(0)
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

describe('lineEstimateMinutes, what one line on the summary shows', () => {
  it('adds the queue of the line station to the time the item itself needs', () => {
    expect(lineEstimateMinutes(QUEUES, 'station-kueche', 8)).toBe(20)
  })

  it('reports nothing for an item nobody gave a preparation time', () => {
    expect(lineEstimateMinutes(QUEUES, 'station-grill', null)).toBeNull()
  })

  it('reports the queue alone for an item whose preparation takes no time at all', () => {
    expect(lineEstimateMinutes(QUEUES, 'station-grill', 0)).toBe(4)
  })

  it('reports nothing while the line still waits for its station', () => {
    expect(lineEstimateMinutes(QUEUES, null, 8)).toBeNull()
  })
})
