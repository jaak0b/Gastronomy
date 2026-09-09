import type { ApiErrorBody } from './apiError'
import { assertNever } from './assertNever'

export type SendFailure =
  | { kind: 'unreachable' }
  | { kind: 'error'; status: number; body: ApiErrorBody | null }

export interface SendFailureMessage {
  key: string
}

function keyForStatus(status: number): string {
  switch (status) {
    case 429:
      return 'session.tooManyRequests'
    case 500:
    case 503:
      return 'review.sendFailedDatabase'
    default:
      return 'review.sendFailed'
  }
}

function keyForRejection(status: number, body: ApiErrorBody | null): string {
  const statedReason = body?.messageKey ?? ''
  return statedReason.length > 0 ? statedReason : keyForStatus(status)
}

export function messageForAnInterruptedSend(): SendFailureMessage {
  return { key: 'review.sendInterrupted' }
}

export function messageForSendFailure(failure: SendFailure): SendFailureMessage {
  switch (failure.kind) {
    case 'unreachable':
      return { key: 'review.sendFailed' }
    case 'error':
      return { key: keyForRejection(failure.status, failure.body) }
    default:
      return assertNever(failure)
  }
}
