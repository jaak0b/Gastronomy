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
  setTableLabel,
} from '../../src/core/draftCart'
import type { DraftLine } from '../../src/core/apiTypes'

function bratwurstLine(): DraftLine {
  return {
    catalogItemId: 'item-1',
    quantity: 1,
    note: null,
    productionLocationId: null,
    name: 'Bratwurst',
    unitPriceCents: 350,
  }
}

describe('emptyDraft', () => {
  it('starts with no table, no note, no lines and no submission id', () => {
    const draft = emptyDraft()

    expect(draft).toEqual({ tableLabel: '', note: null, lines: [], clientOrderId: null })
  })
})

describe('loadDraft', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('returns an empty draft when nothing was ever stored', () => {
    const draft = loadDraft()

    expect(draft).toEqual({ tableLabel: '', note: null, lines: [], clientOrderId: null })
  })

  it('returns an empty draft when the stored value is not readable', () => {
    localStorage.setItem(DRAFT_STORAGE_KEY, 'not json')

    const draft = loadDraft()

    expect(draft).toEqual({ tableLabel: '', note: null, lines: [], clientOrderId: null })
  })

  it('puts a half built order back on the screen after a reload', () => {
    saveDraft({
      tableLabel: 'Tisch 12',
      note: 'ohne Eis',
      lines: [bratwurstLine()],
      clientOrderId: null,
    })

    const draft = loadDraft()

    expect(draft).toEqual({
      tableLabel: 'Tisch 12',
      note: 'ohne Eis',
      lines: [
        {
          catalogItemId: 'item-1',
          quantity: 1,
          note: null,
          productionLocationId: null,
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
      '{"tableLabel":"Tisch 12","note":null,"clientOrderId":null,"lines":[{"catalogItemId":"item-1","quantity":2,"note":null,"productionLocationId":null}]}',
    )

    const draft = loadDraft()

    expect(draft.lines).toEqual([
      {
        catalogItemId: 'item-1',
        quantity: 2,
        note: null,
        productionLocationId: null,
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
    saveDraft({ tableLabel: 'Tisch 3', note: null, lines: [], clientOrderId: null })

    const stored = JSON.parse(localStorage.getItem(DRAFT_STORAGE_KEY) as string) as object

    expect(Object.keys(stored).sort()).toEqual(['clientOrderId', 'lines', 'note', 'tableLabel'])
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
      'productionLocationId',
      'quantity',
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
        productionLocationId: null,
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

    setLineStation(draft, 0, 'location-2')

    expect(loadDraft().lines[0].productionLocationId).toBe('location-2')
  })

  it('leaves the next line without a station after one line got a choice', () => {
    const first = addLine(emptyDraft(), bratwurstLine())
    const chosen = setLineStation(first, 0, 'location-2')

    const second = addLine(chosen, bratwurstLine())

    expect(second.lines[1].productionLocationId).toBeNull()
  })

  it('persists the table', () => {
    setTableLabel(emptyDraft(), 'Tisch 12')

    expect(loadDraft().tableLabel).toBe('Tisch 12')
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
      tableLabel: 'Tisch 12',
      note: null,
      lines: [bratwurstLine()],
      clientOrderId: 'a2f0c0de-0000-4000-8000-000000000001',
    })

    clearDraft()

    expect(localStorage.getItem(DRAFT_STORAGE_KEY)).toBeNull()
  })

  it('leaves the next draft empty', () => {
    saveDraft({
      tableLabel: 'Tisch 12',
      note: null,
      lines: [bratwurstLine()],
      clientOrderId: 'a2f0c0de-0000-4000-8000-000000000001',
    })

    clearDraft()

    expect(loadDraft()).toEqual({ tableLabel: '', note: null, lines: [], clientOrderId: null })
  })
})
