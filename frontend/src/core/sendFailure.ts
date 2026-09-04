import type { ApiErrorBody } from './apiError'
import { assertNever } from './assertNever'

export type SendFailure =
  | { kind: 'unreachable' }
  | { kind: 'error'; status: number; body: ApiErrorBody | null }

export interface SendFailureMessage {
  key: string
  paperFallbackKey: string | null
}

const PAPER_FALLBACK_AFTER_ATTEMPTS = 2

function keyForStatus(status: number): string {
  switch (status) {
    case 429:
      return 'review.tooManyRequests'
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

export function messageForSendFailure(
  failure: SendFailure,
  failedAttempts: number,
): SendFailureMessage {
  const paperFallbackKey =
    failedAttempts >= PAPER_FALLBACK_AFTER_ATTEMPTS ? 'review.sendFailedAgain' : null
  switch (failure.kind) {
    case 'unreachable':
      return { key: 'review.sendFailed', paperFallbackKey }
    case 'error':
      return { key: keyForRejection(failure.status, failure.body), paperFallbackKey }
    default:
      return assertNever(failure)
  }
}
