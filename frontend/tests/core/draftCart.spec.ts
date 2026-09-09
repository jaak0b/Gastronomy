import { beforeEach, describe, expect, it } from 'vitest'
import {
  DRAFT_STORAGE_KEY,
  SEND_PROGRESS_STORAGE_KEY,
  addLine,
  clearDraft,
  emptyDraft,
  removeLine,
  restoreDraft,
  restoreSendProgress,
  saveDraft,
  saveSendProgress,
  setDeliveryMode,
  setLineNote,
  setLineStation,
  setOrderNote,
  setTableName,
} from '../../src/core/draftCart'
import type { DraftLine } from '../../src/core/apiTypes'

function bratwurstLine(): DraftLine {
  return {
    catalogItemId: 'item-1',
    note: null,
    stationId: null,
    name: 'Bratwurst',
    stationName: '',
  }
}

describe('emptyDraft', () => {
  it('starts with no table, no note, no lines, no submission id and no delivery choice', () => {
    const draft = emptyDraft()

    expect(draft).toEqual({
      tableName: '',
      note: null,
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
      draft: { tableName: '', note: null, lines: [], clientOrderId: null, deliveryModes: {} },
    })
  })

  it('reports the half built order it put back on the screen', () => {
    saveDraft({
      tableName: 'Tisch 12',
      note: null,
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
      draft: { tableName: '', note: null, lines: [], clientOrderId: null, deliveryModes: {} },
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

  it('reads a draft written before lines carried a name without crashing', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      '{"tableName":"Tisch 12","note":null,"clientOrderId":null,"lines":[{"catalogItemId":"item-1","note":null,"stationId":null}]}',
    )

    const restoration = restoreDraft()

    expect(restoration.draft.lines).toEqual([
      {
        catalogItemId: 'item-1',
        note: null,
        stationId: null,
        name: '',
        stationName: '',
      },
    ])
  })

  it('reads a draft written while lines still carried a price, and leaves the price behind', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      '{"tableName":"Tisch 12","note":null,"clientOrderId":null,"lines":[{"catalogItemId":"item-1","note":null,"stationId":null,"name":"Bratwurst","unitPriceCents":350}]}',
    )

    const restoration = restoreDraft()

    expect(restoration.draft.lines).toEqual([
      {
        catalogItemId: 'item-1',
        note: null,
        stationId: null,
        name: 'Bratwurst',
        stationName: '',
      },
    ])
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

  it('stores no field beyond the table, the note, the lines, the submission id and the delivery choice', () => {
    saveDraft({
      tableName: 'Tisch 3',
      note: null,
      lines: [],
      clientOrderId: null,
      deliveryModes: {},
    })

    const stored = JSON.parse(localStorage.getItem(DRAFT_STORAGE_KEY) as string) as object

    expect(Object.keys(stored).sort()).toEqual([
      'clientOrderId',
      'deliveryModes',
      'lines',
      'note',
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

  it('persists the order note', () => {
    setOrderNote(emptyDraft(), 'Hinweis fuer die Kueche')

    expect(restoreDraft().draft.note).toBe('Hinweis fuer die Kueche')
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

describe('a stored draft whose delivery choice cannot be read', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('comes back with no delivery choice rather than being thrown away', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      '{"tableName":"Tisch 12","note":null,"clientOrderId":null,"lines":[],"deliveryModes":{"station-kueche":"whenever"}}',
    )

    const restoration = restoreDraft()

    expect(restoration.outcome).toBe('restored')
    expect(restoration.draft.deliveryModes).toEqual({})
  })

  it('reads a draft written before the delivery choice existed', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      '{"tableName":"Tisch 12","note":null,"clientOrderId":null,"lines":[]}',
    )

    const restoration = restoreDraft()

    expect(restoration.outcome).toBe('restored')
    expect(restoration.draft.deliveryModes).toEqual({})
  })
})

describe('clearDraft', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('removes the stored order', () => {
    saveDraft({
      tableName: 'Tisch 12',
      note: null,
      lines: [bratwurstLine()],
      clientOrderId: 'a2f0c0de-0000-4000-8000-000000000001',
      deliveryModes: {},
    })

    clearDraft()

    expect(localStorage.getItem(DRAFT_STORAGE_KEY)).toBeNull()
  })

  it('leaves the next draft empty', () => {
    saveDraft({
      tableName: 'Tisch 12',
      note: null,
      lines: [bratwurstLine()],
      clientOrderId: 'a2f0c0de-0000-4000-8000-000000000001',
      deliveryModes: { 'station-kueche': 'asItComes' },
    })

    clearDraft()

    expect(restoreDraft().draft).toEqual({
      tableName: '',
      note: null,
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
      settleOnSend: false,
      anAttemptWentUnanswered: false,
      failure: null,
    })
  })

  it('puts back what the phone knew about the send when the page was last open', () => {
    const stored = {
      state: 'failed',
      attempts: 2,
      settleOnSend: true,
      anAttemptWentUnanswered: true,
      failure: { key: 'review.sendFailedDatabase' },
    } as const

    saveSendProgress(stored)

    expect(restoreSendProgress()).toEqual(stored)
  })

  it('remembers a send that is still on its way, so a reload cannot make it look untouched', () => {
    saveSendProgress({
      state: 'sending',
      attempts: 1,
      settleOnSend: false,
      anAttemptWentUnanswered: false,
      failure: null,
    })

    expect(restoreSendProgress().state).toBe('sending')
  })

  it('falls back to an untouched order when the record cannot be read at all', () => {
    localStorage.setItem(SEND_PROGRESS_STORAGE_KEY, 'not json')

    expect(restoreSendProgress()).toEqual({
      state: 'idle',
      attempts: 0,
      settleOnSend: false,
      anAttemptWentUnanswered: false,
      failure: null,
    })
  })

  it('goes when the order goes, so the next order starts open for changes', () => {
    saveSendProgress({
      state: 'failed',
      attempts: 2,
      settleOnSend: false,
      anAttemptWentUnanswered: true,
      failure: null,
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

  it('is empty on an order stored before lines kept it, and that order still loads', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      JSON.stringify({
        tableName: 'Tisch 12',
        note: null,
        clientOrderId: null,
        deliveryModes: {},
        lines: [
          { catalogItemId: 'item-1', note: null, stationId: 'station-kueche', name: 'Bratwurst' },
        ],
      }),
    )

    const restoration = restoreDraft()

    expect(restoration.outcome).toBe('restored')
    expect(restoration.draft.lines[0].stationName).toBe('')
  })
})
