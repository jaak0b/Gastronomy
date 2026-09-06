import { describe, expect, it } from 'vitest'
import {
  mergeSlices,
  nextProductionStatus,
  sliceAdvance,
  splitSlices,
  stationFailureKey,
} from '../../src/core/stationBoard'
import type { StationSlice, StationSliceItem } from '../../src/core/apiTypes'

function sliceItem(orderItemId: string, status: StationSliceItem['productionStatus']): StationSliceItem {
  return { orderItemId, itemName: 'Bratwurst', note: null, productionStatus: status }
}

function slice(overrides: Partial<StationSlice>): StationSlice {
  return {
    stationOrderId: 'slice-1',
    globalOrderNumber: 137,
    stationOrderNumber: 12,
    tableName: 'Tisch 3',
    note: null,
    deliveryMode: 'together',
    createdAtUtc: '2026-09-05T18:00:00Z',
    items: [sliceItem('a', 'waiting')],
    ...overrides,
  }
}

describe('nextProductionStatus', () => {
  it('moves a waiting item into preparation', () => {
    expect(nextProductionStatus('waiting')).toBe('inProduction')
  })

  it('moves an item in preparation to ready', () => {
    expect(nextProductionStatus('inProduction')).toBe('finished')
  })

  it('has nowhere to go from ready', () => {
    expect(nextProductionStatus('finished')).toBeNull()
  })
})

describe('mergeSlices', () => {
  const first = slice({ stationOrderId: 'slice-1', stationOrderNumber: 11 })
  const second = slice({ stationOrderId: 'slice-2', stationOrderNumber: 12 })
  const third = slice({ stationOrderId: 'slice-3', stationOrderNumber: 13 })

  it('leaves the orders the answer did not mention exactly where they were', () => {
    const answered = slice({
      stationOrderId: 'slice-2',
      stationOrderNumber: 12,
      items: [sliceItem('a', 'inProduction')],
    })

    const merged = mergeSlices([first, second, third], [answered])

    expect(merged.map((entry) => entry.stationOrderId)).toEqual(['slice-1', 'slice-2', 'slice-3'])
    expect(merged[1].items[0].productionStatus).toBe('inProduction')
  })

  it('drops an order once every one of its items is ready', () => {
    const answered = slice({
      stationOrderId: 'slice-2',
      stationOrderNumber: 12,
      items: [sliceItem('a', 'finished')],
    })

    const merged = mergeSlices([first, second, third], [answered])

    expect(merged.map((entry) => entry.stationOrderId)).toEqual(['slice-1', 'slice-3'])
  })

  it('takes in an order the board had not seen yet and keeps the numbers in order', () => {
    const answered = slice({
      stationOrderId: 'slice-2',
      stationOrderNumber: 12,
      items: [sliceItem('a', 'waiting')],
    })

    const merged = mergeSlices([third, first], [answered])

    expect(merged.map((entry) => entry.stationOrderNumber)).toEqual([11, 12, 13])
  })
})

describe('splitSlices', () => {
  it('keeps a slice that goes out together as one card', () => {
    const board = splitSlices([slice({ items: [sliceItem('a', 'waiting'), sliceItem('b', 'finished')] })])

    expect(board.together).toHaveLength(1)
    expect(board.together[0].items.map((item) => item.orderItemId)).toEqual(['a', 'b'])
    expect(board.single).toEqual([])
  })

  it('turns every unfinished item of an as-it-comes slice into a card of its own', () => {
    const board = splitSlices([
      slice({
        deliveryMode: 'asItComes',
        items: [sliceItem('a', 'waiting'), sliceItem('b', 'inProduction'), sliceItem('c', 'finished')],
      }),
    ])

    expect(board.together).toEqual([])
    expect(board.single.map((card) => card.item.orderItemId)).toEqual(['a', 'b'])
    expect(board.single[0].globalOrderNumber).toBe(137)
    expect(board.single[0].stationOrderNumber).toBe(12)
    expect(board.single[0].tableName).toBe('Tisch 3')
  })

  it('keeps the order the laptop sent, so a gap in the numbers stays visible', () => {
    const board = splitSlices([
      slice({ stationOrderId: 'slice-12', stationOrderNumber: 12 }),
      slice({ stationOrderId: 'slice-14', stationOrderNumber: 14 }),
    ])

    expect(board.together.map((card) => card.stationOrderNumber)).toEqual([12, 14])
  })
})

describe('sliceAdvance, the control that moves a whole order at once', () => {
  it('starts every waiting item when nothing has been started yet', () => {
    const advance = sliceAdvance(slice({ items: [sliceItem('a', 'waiting'), sliceItem('b', 'waiting')] }))

    expect(advance).toEqual({ orderItemIds: ['a', 'b'], status: 'inProduction' })
  })

  it('starts only the items still waiting when some are already being prepared', () => {
    const advance = sliceAdvance(
      slice({ items: [sliceItem('a', 'inProduction'), sliceItem('b', 'waiting')] }),
    )

    expect(advance).toEqual({ orderItemIds: ['b'], status: 'inProduction' })
  })

  it('marks everything ready once every item is being prepared', () => {
    const advance = sliceAdvance(
      slice({ items: [sliceItem('a', 'inProduction'), sliceItem('b', 'inProduction'), sliceItem('c', 'finished')] }),
    )

    expect(advance).toEqual({ orderItemIds: ['a', 'b'], status: 'finished' })
  })

  it('offers nothing once every item is ready', () => {
    expect(sliceAdvance(slice({ items: [sliceItem('a', 'finished')] }))).toBeNull()
  })
})

describe('stationFailureKey, what the tablet says when a change did not go through', () => {
  it('asks to tap again when the laptop could not be reached', () => {
    expect(stationFailureKey({ kind: 'unreachable' })).toBe('station.actionNotReached')
  })

  it('shows the reason the laptop gave when it named one', () => {
    expect(
      stationFailureKey({
        kind: 'error',
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'station.statusAlreadyPassed',
          parameters: {},
          details: null,
        },
        raw: null,
      }),
    ).toBe('station.statusAlreadyPassed')
  })

  it('falls back to a plain failure when the laptop named nothing', () => {
    expect(stationFailureKey({ kind: 'error', status: 500, body: null, raw: null })).toBe(
      'station.actionFailed',
    )
  })
})
