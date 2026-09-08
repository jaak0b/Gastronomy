import { describe, expect, it } from 'vitest'
import {
  changesAreRefusedIn,
  noSendProgress,
  progressAfterALoad,
  sendHasFailedIn,
  sendIsUnderWayIn,
  sendWasAcceptedIn,
} from '../../src/core/sendProgress'

describe('what a send state still allows the waiter to change', () => {
  it('takes every change while nothing has been sent', () => {
    expect(changesAreRefusedIn('idle')).toBe(false)
  })

  it('refuses every change while the order is on its way to the laptop', () => {
    expect(changesAreRefusedIn('sending')).toBe(true)
  })

  it('refuses every change after a failed send, because the laptop may already hold the order', () => {
    expect(changesAreRefusedIn('failed')).toBe(true)
  })

  it('takes changes again once the laptop has accepted, because the order on screen is the next one', () => {
    expect(changesAreRefusedIn('accepted')).toBe(false)
  })
})

describe('what a send state says about the attempt itself', () => {
  it('calls only a running request a send under way', () => {
    expect(sendIsUnderWayIn('sending')).toBe(true)
    expect(sendIsUnderWayIn('idle')).toBe(false)
    expect(sendIsUnderWayIn('failed')).toBe(false)
    expect(sendIsUnderWayIn('accepted')).toBe(false)
  })

  it('calls only a finished attempt without an answer a failure', () => {
    expect(sendHasFailedIn('failed')).toBe(true)
    expect(sendHasFailedIn('idle')).toBe(false)
    expect(sendHasFailedIn('sending')).toBe(false)
    expect(sendHasFailedIn('accepted')).toBe(false)
  })

  it('calls only an order the laptop took an accepted one', () => {
    expect(sendWasAcceptedIn('accepted')).toBe(true)
    expect(sendWasAcceptedIn('idle')).toBe(false)
    expect(sendWasAcceptedIn('sending')).toBe(false)
    expect(sendWasAcceptedIn('failed')).toBe(false)
  })
})

describe('the send progress a freshly loaded page can honestly report', () => {
  it('reports a send that was still on its way as failed, because its answer died with the page', () => {
    const loaded = progressAfterALoad({
      state: 'sending',
      attempts: 1,
      settleOnSend: false,
      failure: null,
    })

    expect(loaded.state).toBe('failed')
  })

  it('says on the screen that the attempt was cut off rather than blaming the WiFi', () => {
    const loaded = progressAfterALoad({
      state: 'sending',
      attempts: 1,
      settleOnSend: false,
      failure: null,
    })

    expect(loaded.failure).toEqual({ key: 'review.sendInterrupted', paperFallbackKey: null })
  })

  it('counts the cut off attempt, so a second failure is the second one and not the first', () => {
    const loaded = progressAfterALoad({
      state: 'sending',
      attempts: 2,
      settleOnSend: false,
      failure: null,
    })

    expect(loaded.attempts).toBe(2)
    expect(loaded.failure?.paperFallbackKey).toBe('review.sendFailedAgain')
  })

  it('keeps the choice the waiter made about paying, so a retry cannot swap it', () => {
    const loaded = progressAfterALoad({
      state: 'sending',
      attempts: 1,
      settleOnSend: true,
      failure: null,
    })

    expect(loaded.settleOnSend).toBe(true)
  })

  it('leaves a send that had already failed exactly as it was', () => {
    const failed = {
      state: 'failed',
      attempts: 1,
      settleOnSend: false,
      failure: { key: 'review.sendFailedDatabase', paperFallbackKey: null },
    } as const

    expect(progressAfterALoad(failed)).toEqual(failed)
  })

  it('leaves an order nobody has sent yet alone', () => {
    expect(progressAfterALoad(noSendProgress())).toEqual(noSendProgress())
  })

  it('drops the arrival notice of an order that was accepted before the page was loaded again', () => {
    expect(
      progressAfterALoad({ state: 'accepted', attempts: 0, settleOnSend: true, failure: null }),
    ).toEqual(noSendProgress())
  })
})
