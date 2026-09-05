import { beforeEach, describe, expect, it, vi } from 'vitest'
import { buildSubmitRequest, ensureClientOrderId } from '../../src/core/submission'
import {
  addLine,
  clearDraft,
  emptyDraft,
  restoreDraft,
  setTableName,
} from '../../src/core/draftCart'

describe('ensureClientOrderId', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('writes a submission id into a draft that has none', () => {
    const sent = ensureClientOrderId(emptyDraft())

    expect(sent.clientOrderId).not.toBeNull()
  })

  it('persists the submission id before the request starts', () => {
    const sent = ensureClientOrderId(emptyDraft())

    expect(restoreDraft().draft.clientOrderId).toBe(sent.clientOrderId)
  })

  it('keeps the same submission id when the server taps send again', () => {
    const firstAttempt = ensureClientOrderId(emptyDraft())

    const retry = ensureClientOrderId(firstAttempt)

    expect(retry.clientOrderId).toBe(firstAttempt.clientOrderId)
  })

  it('keeps the same submission id across a reload', () => {
    const firstAttempt = ensureClientOrderId(emptyDraft())

    const afterReload = ensureClientOrderId(restoreDraft().draft)

    expect(afterReload.clientOrderId).toBe(firstAttempt.clientOrderId)
  })

  it('gives the next order a different submission id once the draft was cleared', () => {
    const accepted = ensureClientOrderId(emptyDraft())
    clearDraft()

    const nextOrder = ensureClientOrderId(emptyDraft())

    expect(nextOrder.clientOrderId).not.toBe(accepted.clientOrderId)
  })

  it('generates a submission id on a phone served over plain http, where randomUUID is missing', () => {
    const randomBytes = crypto.getRandomValues.bind(crypto)
    vi.stubGlobal('crypto', { getRandomValues: randomBytes })
    try {
      const sent = ensureClientOrderId(emptyDraft())

      expect(sent.clientOrderId).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
      )
    } finally {
      vi.unstubAllGlobals()
    }
  })

  it('gives a second order its own submission id', () => {
    const first = ensureClientOrderId(emptyDraft())
    clearDraft()

    const second = ensureClientOrderId(emptyDraft())

    expect(second.clientOrderId).not.toBe(first.clientOrderId)
  })

  it('generates a submission id in the canonical uuid form', () => {
    const sent = ensureClientOrderId(emptyDraft())

    expect(sent.clientOrderId).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/,
    )
  })
})

describe('buildSubmitRequest', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('sends the table, the note and every item with the price the phone showed', () => {
    const withLine = addLine(emptyDraft(), {
      catalogItemId: 'item-1',
      note: 'ohne Zwiebeln',
      stationId: 'station-2',
      name: 'Bratwurst',
      unitPriceCents: 350,
    })
    const ready = ensureClientOrderId(setTableName(withLine, 'Tisch 12'))

    const request = buildSubmitRequest(ready)

    expect(request).toEqual({
      clientOrderId: ready.clientOrderId,
      tableName: 'Tisch 12',
      note: null,
      items: [
        {
          catalogItemId: 'item-1',
          unitPriceCents: 350,
          note: 'ohne Zwiebeln',
          stationId: 'station-2',
        },
      ],
    })
  })

  it('sends no item name, because the laptop keeps the name from its own catalog', () => {
    const withLine = addLine(emptyDraft(), {
      catalogItemId: 'item-1',
      note: null,
      stationId: null,
      name: 'Bratwurst',
      unitPriceCents: 350,
    })
    const ready = ensureClientOrderId(setTableName(withLine, 'Tisch 12'))

    const request = buildSubmitRequest(ready)

    expect(Object.keys(request.items[0]).sort()).toEqual([
      'catalogItemId',
      'note',
      'stationId',
      'unitPriceCents',
    ])
  })

  it('refuses to build a request for a draft that never got a submission id', () => {
    expect(() => buildSubmitRequest(emptyDraft())).toThrow()
  })
})
