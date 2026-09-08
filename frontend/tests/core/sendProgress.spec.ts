import { describe, expect, it } from 'vitest'
import {
  changesAreRefusedIn,
  noSendProgress,
  paperIsTheOnlyWayLeft,
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

  it('takes every change again after the laptop refused the order, because no order was created', () => {
    expect(changesAreRefusedIn('rejected')).toBe(false)
  })
})

describe('what the phone may still offer after an unsuccessful send', () => {
  it('keeps the reason on the screen after the laptop refused the order', () => {
    expect(sendHasFailedIn('rejected')).toBe(true)
  })

  it('calls a refusal neither a send under way nor an accepted order', () => {
    expect(sendIsUnderWayIn('rejected')).toBe(false)
    expect(sendWasAcceptedIn('rejected')).toBe(false)
  })

  it('leaves the waiter with the retry alone after one attempt the laptop never answered', () => {
    expect(paperIsTheOnlyWayLeft('failed', 1)).toBe(false)
  })

  it('offers paper once two attempts have gone unanswered', () => {
    expect(paperIsTheOnlyWayLeft('failed', 2)).toBe(true)
  })

  it('offers no paper after a refusal, because the waiter can fix what the laptop named', () => {
    expect(paperIsTheOnlyWayLeft('rejected', 5)).toBe(false)
  })

  it('offers no paper while nothing has been sent and while an order is on its way', () => {
    expect(paperIsTheOnlyWayLeft('idle', 5)).toBe(false)
    expect(paperIsTheOnlyWayLeft('sending', 5)).toBe(false)
  })

  it('offers no paper for an order the laptop took', () => {
    expect(paperIsTheOnlyWayLeft('accepted', 5)).toBe(false)
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

    expect(loaded.failure).toEqual({ key: 'review.sendInterrupted' })
  })

  it('counts the cut off attempt, so a second failure is the second one and not the first', () => {
    const loaded = progressAfterALoad({
      state: 'sending',
      attempts: 2,
      settleOnSend: false,
      failure: null,
    })

    expect(loaded.attempts).toBe(2)
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
      failure: { key: 'review.sendFailedDatabase' },
    } as const

    expect(progressAfterALoad(failed)).toEqual(failed)
  })

  it('leaves a refusal standing, so the reason the laptop gave is still on the screen', () => {
    const refused = {
      state: 'rejected',
      attempts: 0,
      settleOnSend: false,
      failure: { key: 'order.unknownItem' },
    } as const

    expect(progressAfterALoad(refused)).toEqual(refused)
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
