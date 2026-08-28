import { beforeEach, describe, expect, it } from 'vitest'
import {
  DRAFT_STORAGE_KEY,
  addLine,
  clearDraft,
  emptyDraft,
  loadDraft,
  removeLine,
  saveDraft,
  setLineNote,
  setLineQuantity,
  setLineStation,
  setOrderNote,
  setTableName,
} from '../../src/core/draftCart'
import type { DraftLine } from '../../src/core/apiTypes'

function bratwurstLine(): DraftLine {
  return {
    catalogItemId: 'item-1',
    quantity: 1,
    note: null,
    stationId: null,
    name: 'Bratwurst',
    unitPriceCents: 350,
  }
}

describe('emptyDraft', () => {
  it('starts with no table, no note, no lines and no submission id', () => {
    const draft = emptyDraft()

    expect(draft).toEqual({ tableName: '', note: null, lines: [], clientOrderId: null })
  })
})

describe('loadDraft', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('returns an empty draft when nothing was ever stored', () => {
    const draft = loadDraft()

    expect(draft).toEqual({ tableName: '', note: null, lines: [], clientOrderId: null })
  })

  it('returns an empty draft when the stored value is not readable', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')

    const draft = loadDraft()

    expect(draft).toEqual({ tableName: '', note: null, lines: [], clientOrderId: null })
  })

  it('puts a half built order back on the screen after a reload', () => {
    saveDraft({
      tableName: 'Tisch 12',
      note: 'ohne Eis',
      lines: [bratwurstLine()],
      clientOrderId: null,
    })

    const draft = loadDraft()

    expect(draft).toEqual({
      tableName: 'Tisch 12',
      note: 'ohne Eis',
      lines: [
        {
          catalogItemId: 'item-1',
          quantity: 1,
          note: null,
          stationId: null,
          name: 'Bratwurst',
          unitPriceCents: 350,
        },
      ],
      clientOrderId: null,
    })
  })

  it('reads a draft written before lines carried a name and a price without crashing', () => {
    localStorage.setItem(
      DRAFT_STORAGE_KEY,
      '{"tableName":"Tisch 12","note":null,"clientOrderId":null,"lines":[{"catalogItemId":"item-1","quantity":2,"note":null,"stationId":null}]}',
    )

    const draft = loadDraft()

    expect(draft.lines).toEqual([
      {
        catalogItemId: 'item-1',
        quantity: 2,
        note: null,
        stationId: null,
        name: '',
        unitPriceCents: 0,
      },
    ])
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

  it('stores no field beyond the table, the note, the lines and the submission id', () => {
    saveDraft({ tableName: 'Tisch 3', note: null, lines: [], clientOrderId: null })

    const stored = JSON.parse(localStorage.getItem(DRAFT_STORAGE_KEY) as string) as object

    expect(Object.keys(stored).sort()).toEqual(['clientOrderId', 'lines', 'note', 'tableName'])
  })

  it('stores the item, the quantity, the note, the station and the name and price it was added at', () => {
    addLine(emptyDraft(), bratwurstLine())

    const stored = JSON.parse(localStorage.getItem(DRAFT_STORAGE_KEY) as string) as {
      lines: object[]
    }

    expect(Object.keys(stored.lines[0]).sort()).toEqual([
      'catalogItemId',
      'name',
      'note',
      'quantity',
      'stationId',
      'unitPriceCents',
    ])
  })

  it('keeps the name the item carried when the line was added', () => {
    addLine(emptyDraft(), bratwurstLine())

    expect(loadDraft().lines[0].name).toBe('Bratwurst')
  })

  it('keeps the price the item carried when the line was added', () => {
    addLine(emptyDraft(), bratwurstLine())

    expect(loadDraft().lines[0].unitPriceCents).toBe(350)
  })
})

describe('draft mutators', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('persists an added line without the caller saving it', () => {
    addLine(emptyDraft(), bratwurstLine())

    expect(loadDraft().lines).toEqual([
      {
        catalogItemId: 'item-1',
        quantity: 1,
        note: null,
        stationId: null,
        name: 'Bratwurst',
        unitPriceCents: 350,
      },
    ])
  })

  it('leaves the draft it was given untouched', () => {
    const before = emptyDraft()

    addLine(before, bratwurstLine())

    expect(before.lines).toEqual([])
  })

  it('persists a changed quantity', () => {
    const draft = addLine(emptyDraft(), bratwurstLine())

    setLineQuantity(draft, 0, 3)

    expect(loadDraft().lines[0].quantity).toBe(3)
  })

  it('removes a line when its quantity reaches zero', () => {
    const draft = addLine(emptyDraft(), bratwurstLine())

    setLineQuantity(draft, 0, 0)

    expect(loadDraft().lines).toEqual([])
  })

  it('persists a removed line', () => {
    const draft = addLine(emptyDraft(), bratwurstLine())

    removeLine(draft, 0)

    expect(loadDraft().lines).toEqual([])
  })

  it('persists a line note', () => {
    const draft = addLine(emptyDraft(), bratwurstLine())

    setLineNote(draft, 0, 'ohne Zwiebeln')

    expect(loadDraft().lines[0].note).toBe('ohne Zwiebeln')
  })

  it('persists the station chosen for a line', () => {
    const draft = addLine(emptyDraft(), bratwurstLine())

    setLineStation(draft, 0, 'station-2')

    expect(loadDraft().lines[0].stationId).toBe('station-2')
  })

  it('leaves the next line without a station after one line got a choice', () => {
    const first = addLine(emptyDraft(), bratwurstLine())
    const chosen = setLineStation(first, 0, 'station-2')

    const second = addLine(chosen, bratwurstLine())

    expect(second.lines[1].stationId).toBeNull()
  })

  it('persists the table', () => {
    setTableName(emptyDraft(), 'Tisch 12')

    expect(loadDraft().tableName).toBe('Tisch 12')
  })

  it('persists the order note', () => {
    setOrderNote(emptyDraft(), 'Hinweis fuer die Kueche')

    expect(loadDraft().note).toBe('Hinweis fuer die Kueche')
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
    })

    clearDraft()

    expect(loadDraft()).toEqual({ tableName: '', note: null, lines: [], clientOrderId: null })
  })
})

describe('adding an item that is already in the basket', () => {
  it('raises the quantity of the line it is already on', () => {
    const once = addLine(emptyDraft(), bratwurstLine())

    const twice = addLine(once, bratwurstLine())

    expect(twice.lines).toHaveLength(1)
    expect(twice.lines[0].quantity).toBe(2)
  })

  it('adds the quantity that was handed in rather than one', () => {
    const once = addLine(emptyDraft(), bratwurstLine())

    const more = addLine(once, { ...bratwurstLine(), quantity: 3 })

    expect(more.lines[0].quantity).toBe(4)
  })

  it('keeps a line with a note to itself, because the note names those portions', () => {
    const noted = addLine(emptyDraft(), { ...bratwurstLine(), note: 'ohne Senf' })

    const plain = addLine(noted, bratwurstLine())

    expect(plain.lines).toHaveLength(2)
    expect(plain.lines[0].note).toBe('ohne Senf')
    expect(plain.lines[1].quantity).toBe(1)
  })

  it('keeps the same item apart when it was sent to different stations', () => {
    const kitchen = addLine(emptyDraft(), { ...bratwurstLine(), stationId: 'station-1' })

    const bar = addLine(kitchen, { ...bratwurstLine(), stationId: 'station-2' })

    expect(bar.lines).toHaveLength(2)
  })
})
