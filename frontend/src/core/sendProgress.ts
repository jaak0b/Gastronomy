import { assertNever } from './assertNever'
import { messageForAnInterruptedSend, type SendFailureMessage } from './sendFailure'

export const SEND_STATES = ['idle', 'sending', 'failed', 'rejected', 'accepted'] as const

export type SendState = (typeof SEND_STATES)[number]

const PAPER_FALLBACK_AFTER_ATTEMPTS = 2

export interface SendProgress {
  state: SendState
  attempts: number
  settleOnSend: boolean
  failure: SendFailureMessage | null
}

export function noSendProgress(): SendProgress {
  return { state: 'idle', attempts: 0, settleOnSend: false, failure: null }
}

export function sendIsUnderWayIn(state: SendState): boolean {
  switch (state) {
    case 'idle':
    case 'failed':
    case 'rejected':
    case 'accepted':
      return false
    case 'sending':
      return true
    default:
      return assertNever(state)
  }
}

export function sendHasFailedIn(state: SendState): boolean {
  switch (state) {
    case 'idle':
    case 'sending':
    case 'accepted':
      return false
    case 'failed':
    case 'rejected':
      return true
    default:
      return assertNever(state)
  }
}

export function sendWasAcceptedIn(state: SendState): boolean {
  switch (state) {
    case 'idle':
    case 'sending':
    case 'failed':
    case 'rejected':
      return false
    case 'accepted':
      return true
    default:
      return assertNever(state)
  }
}

export function changesAreRefusedIn(state: SendState): boolean {
  switch (state) {
    case 'idle':
    case 'rejected':
    case 'accepted':
      return false
    case 'sending':
    case 'failed':
      return true
    default:
      return assertNever(state)
  }
}

function theLaptopStayedSilentIn(state: SendState): boolean {
  switch (state) {
    case 'idle':
    case 'sending':
    case 'rejected':
    case 'accepted':
      return false
    case 'failed':
      return true
    default:
      return assertNever(state)
  }
}

export function paperIsTheOnlyWayLeft(state: SendState, attempts: number): boolean {
  return theLaptopStayedSilentIn(state) && attempts >= PAPER_FALLBACK_AFTER_ATTEMPTS
}

export function progressAfterALoad(stored: SendProgress): SendProgress {
  switch (stored.state) {
    case 'idle':
    case 'failed':
    case 'rejected':
      return stored
    case 'accepted':
      return noSendProgress()
    case 'sending':
      return {
        ...stored,
        state: 'failed',
        failure: messageForAnInterruptedSend(),
      }
    default:
      return assertNever(stored.state)
  }
}
