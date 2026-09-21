import { beforeEach, describe, expect, it } from 'vitest'
import {

  DRAFT_STORAGE_KEY,
  SEND_PROGRESS_STORAGE_KEY,
  addLine,
  clearDraft,
  draftIsForAnotherFestival,
  emptyDraft,
  removeLine,
  restoreDraft,
  restoreSendProgress,
  saveDraft,
  saveSendProgress,
  setDeliveryMode,
  setLineNote,
  setLineStation,
  setTableName,
  stampFestival,
} from '../../../src/phone/core/draftCart'
import type { DraftLine } from '../../../src/phone/core/draftCart'
import { PlaceOrderRequest } from '../../../src/shared/api/generatedSchemas'

function bratwurstLine(): DraftLine {
  return {
    catalogItemId: 'item-1',
    note: null,
    stationId: null,
    name: 'Bratwurst',
    stationName: '',
  }
}

function anAttempt(): PlaceOrderRequest {
  return {
    clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
    tableName: 'Tisch 5',
    items: [
      {
        catalogItemId: 'item-1',
        unitPriceCents: 350,
        note: null,
        stationId: null,
        settlement: null,
      },
    ],
    deliveryModes: [],
  }
}

describe('emptyDraft', () => {
  it('starts with no table, no lines, no submission id and no delivery choice', () => {
    const draft = emptyDraft()

    expect(draft).toEqual({
      festivalId: null,
      tableName: '',
      lines: [],
      clientOrderId: null,
      deliveryModes: {},
    })
  })
})

describe('restoreDraft', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('reports that nothing was ever stored', () => {
    const restoration = restoreDraft()

    expect(restoration).toEqual({
      outcome: 'nothingStored',
      draft: {
        festivalId: null,
        tableName: '',
        lines: [],
        clientOrderId: null,
        deliveryModes: {},
      },
    })
  })

  it('reports the half built order it put back on the screen', () => {
    saveDraft({
      festivalId: null,
      tableName: 'Tisch 12',
      lines: [bratwurstLine()],
      clientOrderId: null,
      deliveryModes: {},
    })

    const restoration = restoreDraft()

    expect(restoration.outcome).toBe('restored')
    expect(restoration.draft.lines).toHaveLength(1)
  })

  it('reports that an unreadable draft was thrown away instead of losing it in silence', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')

    const restoration = restoreDraft()

    expect(restoration).toEqual({
      outcome: 'unreadableDraftDiscarded',
      draft: {
        festivalId: null,
        tableName: '',
        lines: [],
        clientOrderId: null,
        deliveryModes: {},
      },
    })
  })

  it('throws the unreadable value away, so the loss is reported once and not on every reload', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')

    restoreDraft()

    expect(localStorage.getItem(DRAFT_STORAGE_KEY)).toBeNull()
  })

  it('reports a stored draft that no longer has the shape of an order as thrown away', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, '{"tableName":12,"lines":[]}')

    const restoration = restoreDraft()

    expect(restoration.outcome).toBe('unreadableDraftDiscarded')
  })

  it('reports a stored draft whose lines are unreadable as thrown away', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, '{"tableName":"Tisch 12","lines":[{"note":"ohne Eis"}]}')

    const restoration = restoreDraft()

    expect(restoration.outcome).toBe('unreadableDraftDiscarded')
  })
})

describe('the stored draft shape', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('stores exactly one order and never a list of orders', () => {
    saveDraft(emptyDraft())

    const stored: unknown = JSON.parse(localStorage.getItem(DRAFT_STORAGE_KEY) as string)

    expect(Array.isArray(stored)).toBe(false)
  })

  it('stores no field beyond the festival, the table, the lines, the submission id and the delivery choice', () => {
    saveDraft({
      festivalId: null,
      tableName: 'Tisch 3',
      lines: [],
      clientOrderId: null,
      deliveryModes: {},
    })

    const stored = JSON.parse(localStorage.getItem(DRAFT_STORAGE_KEY) as string) as object

    expect(Object.keys(stored).sort()).toEqual([
      'clientOrderId',
      'deliveryModes',
      'festivalId',
      'lines',
      'tableName',
    ])
  })

  it('stores the item, the note, the station and the names the two were added under', () => {
    addLine(emptyDraft(), bratwurstLine())

    const stored = JSON.parse(localStorage.getItem(DRAFT_STORAGE_KEY) as string) as {
      lines: object[]
    }

    expect(Object.keys(stored.lines[0]).sort()).toEqual([
      'catalogItemId',
      'name',
      'note',
      'stationId',
      'stationName',
    ])
  })

  it('keeps the name the item carried when the line was added', () => {
    addLine(emptyDraft(), bratwurstLine())

    expect(restoreDraft().draft.lines[0].name).toBe('Bratwurst')
  })

})

describe('draft mutators', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('persists an added line without the caller saving it', () => {
    addLine(emptyDraft(), bratwurstLine())

    expect(restoreDraft().draft.lines).toEqual([
      {
        catalogItemId: 'item-1',
        note: null,
        stationId: null,
        name: 'Bratwurst',
        stationName: '',
      },
    ])
  })

  it('leaves the draft it was given untouched', () => {
    const before = emptyDraft()

    addLine(before, bratwurstLine())

    expect(before.lines).toEqual([])
  })

  it('keeps one position per tap when the same item is added twice', () => {
    const draft = addLine(emptyDraft(), bratwurstLine())

    addLine(draft, bratwurstLine())

    expect(restoreDraft().draft.lines).toHaveLength(2)
  })

  it('persists a removed line', () => {
    const draft = addLine(emptyDraft(), bratwurstLine())

    removeLine(draft, 0)

    expect(restoreDraft().draft.lines).toEqual([])
  })

  it('persists a line note', () => {
    const draft = addLine(emptyDraft(), bratwurstLine())

    setLineNote(draft, 0, 'ohne Zwiebeln')

    expect(restoreDraft().draft.lines[0].note).toBe('ohne Zwiebeln')
  })

  it('persists the station chosen for a line', () => {
    const draft = addLine(emptyDraft(), bratwurstLine())

    setLineStation(draft, 0, 'station-2', 'Theke aussen')

    expect(restoreDraft().draft.lines[0].stationId).toBe('station-2')
  })

  it('leaves the next line without a station after one line got a choice', () => {
    const first = addLine(emptyDraft(), bratwurstLine())
    const chosen = setLineStation(first, 0, 'station-2', 'Theke aussen')

    const second = addLine(chosen, bratwurstLine())

    expect(second.lines[1].stationId).toBeNull()
  })

  it('persists the table', () => {
    setTableName(emptyDraft(), 'Tisch 12')

    expect(restoreDraft().draft.tableName).toBe('Tisch 12')
  })

  it('persists how a station should hand its part of the order out', () => {
    setDeliveryMode(emptyDraft(), 'station-kueche', 'asItComes')

    expect(restoreDraft().draft.deliveryModes).toEqual({ 'station-kueche': 'asItComes' })
  })

  it('keeps the choice made for one station when another station is chosen for', () => {
    const kitchen = setDeliveryMode(emptyDraft(), 'station-kueche', 'asItComes')

    setDeliveryMode(kitchen, 'station-theke', 'together')

    expect(restoreDraft().draft.deliveryModes).toEqual({
      'station-kueche': 'asItComes',
      'station-theke': 'together',
    })
  })
})

describe('clearDraft', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('removes the stored order', () => {
    saveDraft({
      festivalId: null,
      tableName: 'Tisch 12',
      lines: [bratwurstLine()],
      clientOrderId: 'a2f0c0de-0000-4000-8000-000000000001',
      deliveryModes: {},
    })

    clearDraft()

    expect(localStorage.getItem(DRAFT_STORAGE_KEY)).toBeNull()
  })

  it('leaves the next draft empty', () => {
    saveDraft({
      festivalId: null,
      tableName: 'Tisch 12',
      lines: [bratwurstLine()],
      clientOrderId: 'a2f0c0de-0000-4000-8000-000000000001',
      deliveryModes: { 'station-kueche': 'asItComes' },
    })

    clearDraft()

    expect(restoreDraft().draft).toEqual({
      festivalId: null,
      tableName: '',
      lines: [],
      clientOrderId: null,
      deliveryModes: {},
    })
  })
})

describe('adding an item that is already in the basket', () => {
  it('keeps every tap as its own position', () => {
    const once = addLine(emptyDraft(), bratwurstLine())

    const twice = addLine(once, bratwurstLine())

    expect(twice.lines).toHaveLength(2)
  })

  it('keeps a position with a note apart from a position without one', () => {
    const noted = addLine(emptyDraft(), { ...bratwurstLine(), note: 'ohne Senf' })

    const plain = addLine(noted, bratwurstLine())

    expect(plain.lines).toHaveLength(2)
    expect(plain.lines[0].note).toBe('ohne Senf')
    expect(plain.lines[1].note).toBeNull()
  })

  it('keeps the same item apart when it was sent to different stations', () => {
    const kitchen = addLine(emptyDraft(), { ...bratwurstLine(), stationId: 'station-1' })

    const bar = addLine(kitchen, { ...bratwurstLine(), stationId: 'station-2' })

    expect(bar.lines).toHaveLength(2)
  })
})

describe('the record of what became of a send', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('reports an untouched order when the waiter has never pressed send', () => {
    expect(restoreSendProgress()).toEqual({
      state: 'idle',
      attempts: 0,
      failure: null,
      unresolvedAttempt: null,
    })
  })

  it('puts back what the phone knew about the send when the page was last open', () => {
    const stored = {
      state: 'failed',
      attempts: 2,
      failure: { key: 'review.sendFailedDatabase' },
      unresolvedAttempt: anAttempt(),
    } as const

    saveSendProgress(stored)

    expect(restoreSendProgress()).toEqual(stored)
  })

  it('keeps the words a refusal fills in, so its notice still names the item after a reload', () => {
    const stored = {
      state: 'rejected',
      attempts: 2,
      failure: {
        key: 'catalog.itemSoldOut',
        parameters: { name: 'Wasser', catalogItemId: 'item-wasser' },
      },
      unresolvedAttempt: null,
    } as const

    saveSendProgress(stored)

    expect(restoreSendProgress().failure).toEqual(stored.failure)
  })

  it('remembers a send that is still on its way, so a reload cannot make it look untouched', () => {
    const attempt = anAttempt()
    saveSendProgress({
      state: 'sending',
      attempts: 1,
      failure: null,
      unresolvedAttempt: attempt,
    })

    expect(restoreSendProgress().state).toBe('sending')
    expect(restoreSendProgress().unresolvedAttempt).toEqual(attempt)
  })

  it('discards a send that was on its way without the attempt it carried', () => {
    localStorage.setItem(
      SEND_PROGRESS_STORAGE_KEY,
      JSON.stringify({ state: 'sending', attempts: 1, failure: null }),
    )

    expect(restoreSendProgress()).toEqual({
      state: 'idle',
      attempts: 0,
      failure: null,
      unresolvedAttempt: null,
    })
  })

  it('discards a stored attempt that is not a request, so it can never be replayed', () => {
    saveSendProgress({
      state: 'failed',
      attempts: 1,
      failure: null,
      unresolvedAttempt: { tableName: 'Tisch 5' } as unknown as PlaceOrderRequest,
    })

    expect(restoreSendProgress()).toEqual({
      state: 'idle',
      attempts: 0,
      failure: null,
      unresolvedAttempt: null,
    })
  })

  it('falls back to an untouched order when the record cannot be read at all', () => {
    localStorage.setItem(SEND_PROGRESS_STORAGE_KEY, 'not json')

    expect(restoreSendProgress()).toEqual({
      state: 'idle',
      attempts: 0,
      failure: null,
      unresolvedAttempt: null,
    })
  })

  it('goes when the order goes, so the next order starts open for changes', () => {
    saveSendProgress({
      state: 'failed',
      attempts: 2,
      failure: null,
      unresolvedAttempt: anAttempt(),
    })

    clearDraft()

    expect(restoreSendProgress().state).toBe('idle')
  })
})

describe('the station name a line keeps', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('is the name the station had when it was chosen for that line', () => {
    const chosen = setLineStation(addLine(emptyDraft(), bratwurstLine()), 0, 'station-kueche', 'Küche')

    expect(chosen.lines[0].stationName).toBe('Küche')
  })

  it('comes back with the line after a reload', () => {
    saveDraft(setLineStation(addLine(emptyDraft(), bratwurstLine()), 0, 'station-kueche', 'Küche'))

    expect(restoreDraft().draft.lines[0].stationName).toBe('Küche')
  })
})

describe('the festival a draft belongs to', () => {
  it('is remembered across a reload, so the order is not thrown away as another festival', () => {
    saveDraft({ ...emptyDraft(), festivalId: 'fest-1', tableName: 'Tisch 4' })

    expect(restoreDraft().draft.festivalId).toBe('fest-1')
  })

  it('is stamped on the draft and kept in storage', () => {
    const stamped = stampFestival(emptyDraft(), 'fest-1')

    expect(stamped.festivalId).toBe('fest-1')
    expect(restoreDraft().draft.festivalId).toBe('fest-1')
  })

  it('belongs to another festival when the running one differs', () => {
    const draft = { ...emptyDraft(), festivalId: 'fest-1' }

    expect(draftIsForAnotherFestival(draft, 'fest-2')).toBe(true)
    expect(draftIsForAnotherFestival(draft, null)).toBe(true)
    expect(draftIsForAnotherFestival(draft, 'fest-1')).toBe(false)
  })
})

describe('a stored draft this build cannot read', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('is thrown away when it misses the delivery choice field this build writes', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      '{"festivalId":null,"tableName":"Tisch 12","clientOrderId":null,"lines":[]}',
    )

    expect(restoreDraft()).toEqual({
      outcome: 'unreadableDraftDiscarded',
      draft: emptyDraft(),
    })
  })

  it('is thrown away when a line carries a field this build does not know', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      JSON.stringify({
        festivalId: null,
        tableName: 'Tisch 12',
        clientOrderId: null,
        deliveryModes: {},
        lines: [
          {
            catalogItemId: 'item-1',
            note: null,
            stationId: null,
            name: 'Bratwurst',
            stationName: '',
            unitPriceCents: 350,
          },
        ],
      }),
    )

    expect(restoreDraft().outcome).toBe('unreadableDraftDiscarded')
  })

  it('is thrown away when a line misses the name it was added under', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      JSON.stringify({
        festivalId: null,
        tableName: 'Tisch 12',
        clientOrderId: null,
        deliveryModes: {},
        lines: [{ catalogItemId: 'item-1', note: null, stationId: null, stationName: '' }],
      }),
    )

    expect(restoreDraft().outcome).toBe('unreadableDraftDiscarded')
  })

  it('is thrown away when a field holds another type than the one this build writes', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      JSON.stringify({
        festivalId: 7,
        tableName: 'Tisch 12',
        clientOrderId: null,
        deliveryModes: {},
        lines: [],
      }),
    )

    expect(restoreDraft().outcome).toBe('unreadableDraftDiscarded')
  })

  it('is thrown away when the delivery choice names a mode this build no longer has', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      JSON.stringify({
        festivalId: null,
        tableName: 'Tisch 12',
        clientOrderId: null,
        deliveryModes: { 'station-kueche': 'whenever' },
        lines: [],
      }),
    )

    expect(restoreDraft().outcome).toBe('unreadableDraftDiscarded')
  })

  it('is thrown away when the delivery choice is a list instead of a map', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      JSON.stringify({
        festivalId: null,
        tableName: 'Tisch 12',
        clientOrderId: null,
        deliveryModes: [],
        lines: [],
      }),
    )

    expect(restoreDraft().outcome).toBe('unreadableDraftDiscarded')
  })

  it('is thrown away when the stored value is an array', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, '[]')

    expect(restoreDraft().outcome).toBe('unreadableDraftDiscarded')
  })

  it('leaves storage clean, so the next load starts on an empty order', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, '{"tableName":"Tisch 12","lines":[]}')

    restoreDraft()

    expect(localStorage.getItem(DRAFT_STORAGE_KEY)).toBeNull()
  })
})

describe('a stored send record this build cannot read', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('is thrown away when the request it carries misses fields of a request', () => {
    localStorage.setItem(
      SEND_PROGRESS_STORAGE_KEY,
      JSON.stringify({
        state: 'failed',
        attempts: 1,
        failure: null,
        unresolvedAttempt: {
          clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
          tableName: 'Tisch 5',
          items: [],
        },
      }),
    )

    expect(restoreSendProgress().state).toBe('idle')
    expect(restoreSendProgress().unresolvedAttempt).toBeNull()
  })

  it('is thrown away when an item of the carried request holds another type than a request does', () => {
    localStorage.setItem(
      SEND_PROGRESS_STORAGE_KEY,
      JSON.stringify({
        state: 'failed',
        attempts: 1,
        failure: null,
        unresolvedAttempt: {
          clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
          tableName: 'Tisch 5',
          items: [
            {
              catalogItemId: 'item-wasser',
              unitPriceCents: '350',
              note: null,
              stationId: null,
              settlement: null,
            },
          ],
          deliveryModes: [],
        },
      }),
    )

    expect(restoreSendProgress().state).toBe('idle')
  })

  it('is thrown away when an item of the carried request settles a line in an unknown shape', () => {
    localStorage.setItem(
      SEND_PROGRESS_STORAGE_KEY,
      JSON.stringify({
        state: 'failed',
        attempts: 1,
        failure: null,
        unresolvedAttempt: {
          clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
          tableName: 'Tisch 5',
          items: [
            {
              catalogItemId: 'item-wasser',
              unitPriceCents: 200,
              note: null,
              stationId: null,
              settlement: { paidPriceCents: 200 },
            },
          ],
          deliveryModes: [],
        },
      }),
    )

    expect(restoreSendProgress().state).toBe('idle')
  })

  it('is thrown away when a carried delivery mode is not one this build has', () => {
    localStorage.setItem(
      SEND_PROGRESS_STORAGE_KEY,
      JSON.stringify({
        state: 'failed',
        attempts: 1,
        failure: null,
        unresolvedAttempt: {
          clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
          tableName: 'Tisch 5',
          items: [],
          deliveryModes: [{ stationId: 'station-kueche', deliveryMode: 'whenever' }],
        },
      }),
    )

    expect(restoreSendProgress().state).toBe('idle')
  })

  it('is thrown away when the reason fills its sentence with a value this build cannot read', () => {
    localStorage.setItem(
      SEND_PROGRESS_STORAGE_KEY,
      JSON.stringify({
        state: 'rejected',
        attempts: 1,
        failure: {
          key: 'catalog.itemSoldOut',
          parameters: { name: { text: 'Wasser' }, catalogItemId: 'item-wasser' },
        },
        unresolvedAttempt: null,
      }),
    )

    expect(restoreSendProgress().state).toBe('idle')
  })

  it('is thrown away when the reason has no key', () => {
    localStorage.setItem(
      SEND_PROGRESS_STORAGE_KEY,
      JSON.stringify({
        state: 'rejected',
        attempts: 1,
        failure: { parameters: { name: 'Wasser' } },
        unresolvedAttempt: null,
      }),
    )

    expect(restoreSendProgress().state).toBe('idle')
  })

  it('is thrown away when the reason fills its sentence with a list instead of named values', () => {
    localStorage.setItem(
      SEND_PROGRESS_STORAGE_KEY,
      JSON.stringify({
        state: 'rejected',
        attempts: 1,
        failure: { key: 'catalog.itemSoldOut', parameters: ['Wasser', 'item-wasser'] },
        unresolvedAttempt: null,
      }),
    )

    expect(restoreSendProgress().state).toBe('idle')
  })

  it('is thrown away when the number of attempts is not a count', () => {
    localStorage.setItem(
      SEND_PROGRESS_STORAGE_KEY,
      JSON.stringify({
        state: 'failed',
        attempts: -1,
        failure: null,
        unresolvedAttempt: {
          clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
          tableName: 'Tisch 5',
          items: [],
          deliveryModes: [],
        },
      }),
    )

    expect(restoreSendProgress().state).toBe('idle')
  })

  it('is thrown away when it carries a field this build does not know', () => {
    localStorage.setItem(
      SEND_PROGRESS_STORAGE_KEY,
      JSON.stringify({
        state: 'rejected',
        attempts: 1,
        failure: { key: 'order.unknownItem' },
        unresolvedAttempt: null,
        answeredAtUtc: '2026-01-01T00:00:00Z',
      }),
    )

    expect(restoreSendProgress().state).toBe('idle')
  })
})
