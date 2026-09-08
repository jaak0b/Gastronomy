import type { ApiErrorBody } from './apiError'
import { assertNever } from './assertNever'

export type SendFailure =
  | { kind: 'unreachable' }
  | { kind: 'error'; status: number; body: ApiErrorBody | null }

export interface SendFailureMessage {
  key: string
}

function statedReason(body: ApiErrorBody | null): string {
  return body?.messageKey ?? ''
}

export function theLaptopNamedAReason(body: ApiErrorBody | null): boolean {
  return statedReason(body).length > 0
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
  return theLaptopNamedAReason(body) ? statedReason(body) : keyForStatus(status)
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
