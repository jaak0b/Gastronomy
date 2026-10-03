import { beforeEach, describe, expect, it } from 'vitest'
import { DRAFT_STORAGE_KEY, SEND_PROGRESS_STORAGE_KEY, emptyDraft, restoreDraft, restoreSendProgress } from '../../../src/phone/core/draftCart'

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
          key: 'errors.order.itemSoldOut',
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
        failure: { key: 'errors.order.itemSoldOut', parameters: ['Wasser', 'item-wasser'] },
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
        failure: { key: 'errors.order.unknownItem' },
        unresolvedAttempt: null,
        answeredAtUtc: '2026-01-01T00:00:00Z',
      }),
    )

    expect(restoreSendProgress().state).toBe('idle')
  })
})
