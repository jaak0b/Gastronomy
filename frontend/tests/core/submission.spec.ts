import { beforeEach, describe, expect, it } from 'vitest'
import { buildSubmitRequest, ensureClientOrderId } from '../../src/core/submission'
import { addLine, clearDraft, emptyDraft, loadDraft, setTableLabel } from '../../src/core/draftCart'

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

    expect(loadDraft().clientOrderId).toBe(sent.clientOrderId)
  })

  it('keeps the same submission id when the server taps send again', () => {
    const firstAttempt = ensureClientOrderId(emptyDraft())

    const retry = ensureClientOrderId(firstAttempt)

    expect(retry.clientOrderId).toBe(firstAttempt.clientOrderId)
  })

  it('keeps the same submission id across a reload', () => {
    const firstAttempt = ensureClientOrderId(emptyDraft())

    const afterReload = ensureClientOrderId(loadDraft())

    expect(afterReload.clientOrderId).toBe(firstAttempt.clientOrderId)
  })

  it('gives the next order a different submission id once the draft was cleared', () => {
    const accepted = ensureClientOrderId(emptyDraft())
    clearDraft()

    const nextOrder = ensureClientOrderId(emptyDraft())

    expect(nextOrder.clientOrderId).not.toBe(accepted.clientOrderId)
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

  it('sends the table, the note, the lines and the total the server was shown', () => {
    const withLine = addLine(emptyDraft(), {
      catalogItemId: 'item-1',
      quantity: 2,
      note: 'ohne Zwiebeln',
      productionLocationId: 'location-2',
      name: 'Bratwurst',
      unitPriceCents: 350,
    })
    const ready = ensureClientOrderId(setTableLabel(withLine, 'Tisch 12'))

    const request = buildSubmitRequest(ready, 1050)

    expect(request).toEqual({
      clientOrderId: ready.clientOrderId,
      tableLabel: 'Tisch 12',
      note: null,
      expectedTotalCents: 1050,
      lines: [
        {
          catalogItemId: 'item-1',
          quantity: 2,
          note: 'ohne Zwiebeln',
          productionLocationId: 'location-2',
        },
      ],
    })
  })

  it('sends no name and no price, because the laptop is the authority on both', () => {
    const withLine = addLine(emptyDraft(), {
      catalogItemId: 'item-1',
      quantity: 1,
      note: null,
      productionLocationId: null,
      name: 'Bratwurst',
      unitPriceCents: 350,
    })
    const ready = ensureClientOrderId(setTableLabel(withLine, 'Tisch 12'))

    const request = buildSubmitRequest(ready, 350)

    expect(Object.keys(request.lines[0]).sort()).toEqual([
      'catalogItemId',
      'note',
      'productionLocationId',
      'quantity',
    ])
  })

  it('refuses to build a request for a draft that never got a submission id', () => {
    expect(() => buildSubmitRequest(emptyDraft(), 0)).toThrow()
  })
})
