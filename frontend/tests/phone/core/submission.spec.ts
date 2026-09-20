import { beforeEach, describe, expect, it, vi } from 'vitest'
import { buildSubmitRequest, withClientOrderIdIfMissing } from '../../../src/phone/core/submission'
import type { Catalog } from '../../../src/shared/api/apiTypes'
import {
  addLine,
  clearDraft,
  emptyDraft,
  restoreDraft,
  setTableName,
} from '../../../src/phone/core/draftCart'

describe('withClientOrderIdIfMissing', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('writes a submission id into a draft that has none', () => {
    const sent = withClientOrderIdIfMissing(emptyDraft())

    expect(sent.clientOrderId).not.toBeNull()
  })

  it('persists the submission id before the request starts', () => {
    const sent = withClientOrderIdIfMissing(emptyDraft())

    expect(restoreDraft().draft.clientOrderId).toBe(sent.clientOrderId)
  })

  it('keeps the same submission id when the server taps send again', () => {
    const firstAttempt = withClientOrderIdIfMissing(emptyDraft())

    const retry = withClientOrderIdIfMissing(firstAttempt)

    expect(retry.clientOrderId).toBe(firstAttempt.clientOrderId)
  })

  it('keeps the same submission id across a reload', () => {
    const firstAttempt = withClientOrderIdIfMissing(emptyDraft())

    const afterReload = withClientOrderIdIfMissing(restoreDraft().draft)

    expect(afterReload.clientOrderId).toBe(firstAttempt.clientOrderId)
  })

  it('gives the next order a different submission id once the draft was cleared', () => {
    const accepted = withClientOrderIdIfMissing(emptyDraft())
    clearDraft()

    const nextOrder = withClientOrderIdIfMissing(emptyDraft())

    expect(nextOrder.clientOrderId).not.toBe(accepted.clientOrderId)
  })

  it('generates a submission id on a phone served over plain http, where randomUUID is missing', () => {
    const randomBytes = crypto.getRandomValues.bind(crypto)
    vi.stubGlobal('crypto', { getRandomValues: randomBytes })
    try {
      const sent = withClientOrderIdIfMissing(emptyDraft())

      expect(sent.clientOrderId).toMatch(
        /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/,
      )
    } finally {
      vi.unstubAllGlobals()
    }
  })

  it('gives a second order its own submission id', () => {
    const first = withClientOrderIdIfMissing(emptyDraft())
    clearDraft()

    const second = withClientOrderIdIfMissing(emptyDraft())

    expect(second.clientOrderId).not.toBe(first.clientOrderId)
  })

  it('generates a submission id in the canonical uuid form', () => {
    const sent = withClientOrderIdIfMissing(emptyDraft())

    expect(sent.clientOrderId).toMatch(
      /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/,
    )
  })
})

describe('buildSubmitRequest', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  function catalog(priceCents = 350): Catalog {
    return {
      categories: [
        { categoryId: 'category-essen', name: 'Essen', colourHex: '#FFEB3B', sortOrder: 1 },
      ],
      items: [
        {
          id: 'item-1',
          name: 'Bratwurst',
          categoryId: 'category-essen',
          priceCents,
          sortOrder: 1,
          isAvailable: true,
          stationIds: ['station-2'],
          productionMinutes: null,
          isQueueIndependent: false,
        },
      ],
      stations: [{ id: 'station-2', name: 'Kueche', sortOrder: 1 }],
    }
  }

  function draftWithABratwurst(note: string | null = null, stationId: string | null = 'station-2') {
    const withLine = addLine(emptyDraft(), {
      catalogItemId: 'item-1',
      note,
      stationId,
      name: 'Bratwurst',
    })
    return withClientOrderIdIfMissing(setTableName(withLine, 'Tisch 12'))
  }

  it('sends the table, the note and every item with the price the phone showed', () => {
    const ready = draftWithABratwurst('ohne Zwiebeln')

    const request = buildSubmitRequest(ready, catalog(), null, [
      { stationId: 'station-2', deliveryMode: 'together' },
    ])

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
          settlement: null,
        },
      ],
      deliveryModes: [{ stationId: 'station-2', deliveryMode: 'together' }],
    })
  })

  it('sends the table name with the spaces around it cut off', () => {
    const ready = setTableName(draftWithABratwurst(), ' Tisch 12 ')

    const request = buildSubmitRequest(ready, catalog(), null, [])

    expect(request.tableName).toBe('Tisch 12')
  })

  it('takes the price from the item list the laptop pushed out, not from the line', () => {
    const ready = draftWithABratwurst()

    const request = buildSubmitRequest(ready, catalog(420), null, [])

    expect(request.items[0].unitPriceCents).toBe(420)
  })

  it('sends the delivery choice the server made for each station', () => {
    const ready = draftWithABratwurst()

    const request = buildSubmitRequest(ready, catalog(), null, [
      { stationId: 'station-2', deliveryMode: 'asItComes' },
    ])

    expect(request.deliveryModes).toEqual([
      { stationId: 'station-2', deliveryMode: 'asItComes' },
    ])
  })

  it('sends no item name, because the laptop keeps the name from its own catalog', () => {
    const ready = draftWithABratwurst(null, null)

    const request = buildSubmitRequest(ready, catalog(), null, [])

    expect(Object.keys(request.items[0]).sort()).toEqual([
      'catalogItemId',
      'note',
      'settlement',
      'stationId',
      'unitPriceCents',
    ])
  })

  it('tells the laptop what the table paid and why it paid less, one paid price per line', () => {
    const ready = draftWithABratwurst(null, null)

    const request = buildSubmitRequest(
      ready,
      catalog(),
      { amountPaidCents: 300, paymentNotice: 'Stammgast' },
      [],
    )

    expect(request.items[0].settlement).toEqual({
      paidPriceCents: 300,
      paymentNotice: 'Stammgast',
    })
  })

  it('gives every item its own share of what the table paid', () => {
    const twoItems: Catalog = {
      ...catalog(),
      items: [
        ...catalog().items,
        {
          id: 'item-2',
          name: 'Bier',
          categoryId: 'category-essen',
          priceCents: 150,
          sortOrder: 2,
          isAvailable: true,
          stationIds: ['station-2'],
          productionMinutes: null,
          isQueueIndependent: false,
        },
      ],
    }
    const withTwoLines = withClientOrderIdIfMissing(
      addLine(draftWithABratwurst(), {
        catalogItemId: 'item-2',
        note: null,
        stationId: 'station-2',
        name: 'Bier',
      }),
    )

    const request = buildSubmitRequest(
      withTwoLines,
      twoItems,
      { amountPaidCents: 300, paymentNotice: null },
      [],
    )

    expect(request.items.map((item) => item.unitPriceCents)).toEqual([350, 150])
    expect(request.items.map((item) => item.settlement)).toEqual([
      { paidPriceCents: 210, paymentNotice: null },
      { paidPriceCents: 90, paymentNotice: null },
    ])
  })

  it('refuses to build a request for a draft that never got a submission id', () => {
    expect(() => buildSubmitRequest(emptyDraft(), catalog(), null, [])).toThrow()
  })
})
