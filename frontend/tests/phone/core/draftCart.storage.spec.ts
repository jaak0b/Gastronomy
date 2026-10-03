import { beforeEach, describe, expect, it } from 'vitest'
import { DRAFT_STORAGE_KEY, addLine, draftIsForAnotherFestival, emptyDraft, restoreDraft, saveDraft, stampFestival } from '../../../src/phone/core/draftCart'
import { bratwurstLine } from './draftCartFixture'

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
