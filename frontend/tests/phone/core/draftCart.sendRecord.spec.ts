import { beforeEach, describe, expect, it } from 'vitest'
import { SEND_PROGRESS_STORAGE_KEY, clearDraft, restoreSendProgress, saveSendProgress } from '../../../src/phone/core/draftCart'
import { PlaceOrderRequest } from '../../../src/shared/api/generatedSchemas'
import { anAttempt } from './draftCartFixture'

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
      failure: { key: 'errors.storage.databaseUnavailable' },
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
        key: 'errors.order.itemSoldOut',
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
