import { describe, expect, it } from 'vitest'
import {
  changesAreRefusedFor,
  noSendProgress,
  paperIsTheOnlyWayLeft,
  progressAfterALoad,
  sendHasFailedIn,
  sendIsUnderWayIn,
  sendWasAcceptedIn,
  type SendProgress,
  type SendState,
} from '../../src/core/sendProgress'

function sendThatIs(state: SendState, anAttemptWentUnanswered = false): SendProgress {
  return { ...noSendProgress(), state, anAttemptWentUnanswered }
}

describe('what the phone knows about a send still allows the waiter to change', () => {
  it('takes every change while nothing has been sent', () => {
    expect(changesAreRefusedFor(sendThatIs('idle'))).toBe(false)
  })

  it('refuses every change while the order is on its way to the laptop', () => {
    expect(changesAreRefusedFor(sendThatIs('sending'))).toBe(true)
  })

  it('refuses every change after a failed send, because the laptop may already hold the order', () => {
    expect(changesAreRefusedFor(sendThatIs('failed'))).toBe(true)
  })

  it('takes changes again once the laptop has accepted, because the order on screen is the next one', () => {
    expect(changesAreRefusedFor(sendThatIs('accepted'))).toBe(false)
  })

  it('takes every change again after the laptop refused the order, because no order was created', () => {
    expect(changesAreRefusedFor(sendThatIs('rejected'))).toBe(false)
  })
})

describe('an order one attempt was left unanswered on', () => {
  it('refuses every change even after the laptop has refused a later attempt', () => {
    expect(changesAreRefusedFor(sendThatIs('rejected', true))).toBe(true)
  })

  it('refuses every change even where the reason has just left the screen', () => {
    expect(changesAreRefusedFor(sendThatIs('idle', true))).toBe(true)
  })

  it('takes changes again once the laptop has accepted it, because that order is done with', () => {
    expect(changesAreRefusedFor(sendThatIs('accepted', true))).toBe(false)
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
    expect(paperIsTheOnlyWayLeft({ ...sendThatIs('failed', true), attempts: 1 })).toBe(false)
  })

  it('offers paper once two attempts have gone unanswered', () => {
    expect(paperIsTheOnlyWayLeft({ ...sendThatIs('failed', true), attempts: 2 })).toBe(true)
  })

  it('offers no paper after a refusal the waiter is still allowed to put right', () => {
    expect(paperIsTheOnlyWayLeft({ ...sendThatIs('rejected'), attempts: 5 })).toBe(false)
  })

  it('offers no paper while nothing has been sent and while an order is on its way', () => {
    expect(paperIsTheOnlyWayLeft({ ...sendThatIs('idle', true), attempts: 5 })).toBe(false)
    expect(paperIsTheOnlyWayLeft({ ...sendThatIs('sending', true), attempts: 5 })).toBe(false)
  })

  it('offers no paper for an order the laptop took', () => {
    expect(paperIsTheOnlyWayLeft({ ...sendThatIs('accepted'), attempts: 5 })).toBe(false)
  })
})

describe('an order the laptop refused although it may already hold it', () => {
  it('offers paper on the first refusal, because the waiter is not allowed to change the order', () => {
    expect(paperIsTheOnlyWayLeft({ ...sendThatIs('rejected', true), attempts: 1 })).toBe(true)
  })

  it('offers paper however few attempts were made, because a second refusal says the same thing', () => {
    expect(paperIsTheOnlyWayLeft({ ...sendThatIs('rejected', true), attempts: 0 })).toBe(true)
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
      anAttemptWentUnanswered: false,
      failure: null,
    })

    expect(loaded.state).toBe('failed')
  })

  it('says on the screen that the attempt was cut off rather than blaming the WiFi', () => {
    const loaded = progressAfterALoad({
      state: 'sending',
      attempts: 1,
      settleOnSend: false,
      anAttemptWentUnanswered: false,
      failure: null,
    })

    expect(loaded.failure).toEqual({ key: 'review.sendInterrupted' })
  })

  it('counts the cut off attempt as one the laptop never answered, so the order stays closed', () => {
    const loaded = progressAfterALoad({
      state: 'sending',
      attempts: 1,
      settleOnSend: false,
      anAttemptWentUnanswered: false,
      failure: null,
    })

    expect(changesAreRefusedFor({ ...loaded, state: 'rejected' })).toBe(true)
  })

  it('counts the cut off attempt, so a second failure is the second one and not the first', () => {
    const loaded = progressAfterALoad({
      state: 'sending',
      attempts: 2,
      settleOnSend: false,
      anAttemptWentUnanswered: false,
      failure: null,
    })

    expect(loaded.attempts).toBe(2)
  })

  it('keeps the choice the waiter made about paying, so a retry cannot swap it', () => {
    const loaded = progressAfterALoad({
      state: 'sending',
      attempts: 1,
      settleOnSend: true,
      anAttemptWentUnanswered: false,
      failure: null,
    })

    expect(loaded.settleOnSend).toBe(true)
  })

  it('leaves a send that had already failed exactly as it was', () => {
    const failed = {
      state: 'failed',
      attempts: 1,
      settleOnSend: false,
      anAttemptWentUnanswered: true,
      failure: { key: 'review.sendFailedDatabase' },
    } as const

    expect(progressAfterALoad(failed)).toEqual(failed)
  })

  it('leaves a refusal standing, so the reason the laptop gave is still on the screen', () => {
    const refused = {
      state: 'rejected',
      attempts: 0,
      settleOnSend: false,
      anAttemptWentUnanswered: false,
      failure: { key: 'order.unknownItem' },
    } as const

    expect(progressAfterALoad(refused)).toEqual(refused)
  })

  it('leaves an order nobody has sent yet alone', () => {
    expect(progressAfterALoad(noSendProgress())).toEqual(noSendProgress())
  })

  it('drops the arrival notice of an order that was accepted before the page was loaded again', () => {
    expect(
      progressAfterALoad({
        state: 'accepted',
        attempts: 0,
        settleOnSend: true,
        anAttemptWentUnanswered: false,
        failure: null,
      }),
    ).toEqual(noSendProgress())
  })
})
