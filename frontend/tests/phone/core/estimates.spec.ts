import { describe, expect, it } from 'vitest'
import {
  pickerEstimateRange,
  queuedMinutesAt,
  stationEstimateAfterAdding,
  stationReadyInMinutes,
} from '../../../src/phone/core/estimates'
import type { CatalogItem, StationEstimate } from '../../../src/shared/api/apiTypes'
import type { BasketLineView } from '../../../src/phone/core/basket'

const QUEUES: StationEstimate[] = [
  { stationId: 'station-kueche', queuedMinutes: 12 },
  { stationId: 'station-grill', queuedMinutes: 50 },
]

const PICKER_QUEUES: StationEstimate[] = [
  { stationId: 'station-kueche', queuedMinutes: 0 },
  { stationId: 'station-grill', queuedMinutes: 50 },
]

function line(overrides: Partial<BasketLineView> = {}): BasketLineView {
  return {
    catalogItemId: 'item-bratwurst',
    name: 'Bratwurst',
    unitPriceCents: 350,
    note: null,
    stationId: 'station-kueche',
    stationName: 'Küche',
    candidateStationIds: ['station-kueche'],
    productionMinutes: 8,
    isQueueIndependent: false,
    isSoldOut: false,
    isNoLongerOnTheMenu: false,
    isNoLongerPreparedAtItsStation: false,
    ...overrides,
  }
}

function item(
  stationIds: string[],
  productionMinutes: number | null,
  isQueueIndependent = false,
): CatalogItem {
  return {
    id: 'item-1',
    name: 'Bratwurst',
    categoryId: 'category-essen',
    priceCents: 350,
    sortOrder: 1,
    isAvailable: true,
    stationIds,
    productionMinutes,
    isQueueIndependent,
  }
}

describe('queuedMinutesAt', () => {
  it('finds the queue of the station', () => {
    expect(queuedMinutesAt(QUEUES, 'station-grill')).toBe(50)
  })

  it('counts a station the laptop said nothing about as having no queue', () => {
    expect(queuedMinutesAt(QUEUES, 'station-unknown')).toBe(0)
  })
})

describe('stationReadyInMinutes, what one station will take for the whole order', () => {
  it('adds the queue to every line that goes to the station', () => {
    const lines = [
      line({ productionMinutes: 8 }),
      line({ catalogItemId: 'item-pommes', name: 'Pommes', productionMinutes: 3 }),
    ]

    expect(stationReadyInMinutes(QUEUES, lines, 'station-kueche')).toBe(23)
  })

  it('counts two portions of the same item as two lines', () => {
    expect(stationReadyInMinutes(QUEUES, [line(), line()], 'station-kueche')).toBe(28)
  })

  it('counts a line nobody gave a time for as adding nothing', () => {
    expect(
      stationReadyInMinutes(QUEUES, [line({ productionMinutes: null })], 'station-kueche'),
    ).toBe(12)
  })

  it('names the queue alone when the station has no timed line at all', () => {
    expect(
      stationReadyInMinutes(QUEUES, [line({ productionMinutes: null })], 'station-grill'),
    ).toBe(50)
  })

  it('names nothing when there is neither a queue nor a line with a time', () => {
    expect(
      stationReadyInMinutes([], [line({ productionMinutes: null })], 'station-kueche'),
    ).toBeNull()
  })

  it('names nothing for a line that still waits for its station', () => {
    const undecided = line({
      stationId: null,
      candidateStationIds: ['station-kueche', 'station-grill'],
      productionMinutes: 8,
    })

    expect(stationReadyInMinutes([], [undecided], 'station-kueche')).toBeNull()
  })
})

describe('stationEstimateAfterAdding, what one station button promises', () => {
  it('adds the queue, the basket already routed there and the item itself', () => {
    const alreadyThere = line({
      stationId: 'station-grill',
      candidateStationIds: ['station-grill'],
      productionMinutes: 2,
    })

    expect(
      stationEstimateAfterAdding(PICKER_QUEUES, [alreadyThere], 'station-grill', 10, false),
    ).toBe(62)
  })

  it('counts every portion that moves to the station', () => {
    expect(stationEstimateAfterAdding(PICKER_QUEUES, [], 'station-grill', 10, false, 2)).toBe(70)
  })

  it('counts an independent candidate once no matter how many units join', () => {
    expect(stationEstimateAfterAdding(PICKER_QUEUES, [], 'station-grill', 10, true, 3)).toBe(10)
  })
})

describe('a line that is prepared independently of the queue', () => {
  it('ignores the queue and counts its own time once across its units', () => {
    const lines = [
      line({ isQueueIndependent: true, productionMinutes: 8 }),
      line({ isQueueIndependent: true, productionMinutes: 8 }),
    ]

    expect(stationReadyInMinutes(QUEUES, lines, 'station-kueche')).toBe(8)
  })

  it('takes the longest of the independent lines', () => {
    const lines = [
      line({ isQueueIndependent: true, productionMinutes: 8 }),
      line({
        catalogItemId: 'item-pommes',
        name: 'Pommes',
        isQueueIndependent: true,
        productionMinutes: 21,
      }),
    ]

    expect(stationReadyInMinutes(QUEUES, lines, 'station-kueche')).toBe(21)
  })

  it('lets a long independent line outlast the shared lane', () => {
    const lines = [
      line({ productionMinutes: 2 }),
      line({
        catalogItemId: 'item-pommes',
        name: 'Pommes',
        isQueueIndependent: true,
        productionMinutes: 30,
      }),
    ]

    expect(stationReadyInMinutes(QUEUES, lines, 'station-kueche')).toBe(30)
  })

  it('lets the shared lane outlast a short independent line', () => {
    const lines = [
      line({ productionMinutes: 8 }),
      line({
        catalogItemId: 'item-pommes',
        name: 'Pommes',
        isQueueIndependent: true,
        productionMinutes: 5,
      }),
    ]

    expect(stationReadyInMinutes(QUEUES, lines, 'station-kueche')).toBe(20)
  })

  it('names nothing when an independent line has no time and nothing is queued', () => {
    expect(
      stationReadyInMinutes(
        [],
        [line({ isQueueIndependent: true, productionMinutes: null })],
        'station-kueche',
      ),
    ).toBeNull()
  })

  it('keeps an explicit zero as a stated time, not as nothing', () => {
    expect(
      stationReadyInMinutes(
        QUEUES,
        [line({ isQueueIndependent: true, productionMinutes: 0 })],
        'station-kueche',
      ),
    ).toBe(0)
  })
})

describe('pickerEstimateRange, what the item list shows before a station is chosen', () => {
  it('spans from the quickest station to the slowest one', () => {
    expect(
      pickerEstimateRange(
        item(['station-kueche', 'station-grill'], 10),
        PICKER_QUEUES,
        [],
      ),
    ).toEqual({ min: 10, max: 60 })
  })

  it('shifts the range with the basket already routed to a station', () => {
    const alreadyThere = line({
      stationId: 'station-grill',
      candidateStationIds: ['station-grill'],
      productionMinutes: 2,
    })

    expect(
      pickerEstimateRange(
        item(['station-kueche', 'station-grill'], 10),
        PICKER_QUEUES,
        [alreadyThere],
      ),
    ).toEqual({ min: 10, max: 62 })
  })

  it('names the one station when the item can only go there', () => {
    expect(pickerEstimateRange(item(['station-kueche'], 10), PICKER_QUEUES, [])).toEqual({
      min: 10,
      max: 10,
    })
  })

  it('names nothing for an item nobody gave a production time', () => {
    expect(pickerEstimateRange(item(['station-kueche'], null), PICKER_QUEUES, [])).toBeNull()
  })

  it('names nothing for an item no station prepares', () => {
    expect(pickerEstimateRange(item([], 10), PICKER_QUEUES, [])).toBeNull()
  })

  it('promises the independent time once, without the queue', () => {
    expect(
      pickerEstimateRange(item(['station-kueche', 'station-grill'], 10, true), PICKER_QUEUES, []),
    ).toEqual({ min: 10, max: 10 })
  })
})
