import { beforeEach, describe, expect, it } from 'vitest'
import { DRAFT_STORAGE_KEY, addLine, clearDraft, emptyDraft, removeLine, restoreDraft, saveDraft, setDeliveryMode, setLineNote, setLineStation, setTableName } from '../../../src/phone/core/draftCart'
import { bratwurstLine } from './draftCartFixture'

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
