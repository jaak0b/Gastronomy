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

function keyForRejection(status: number): string {
  switch (status) {
    case 429:
      return 'review.tooManyRequests'
    case 409:
      return 'review.duplicateRisk'
    case 500:
    case 503:
      return 'review.sendFailedDatabase'
    default:
      return 'review.sendFailed'
  }
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
      return { key: keyForRejection(failure.status), paperFallbackKey }
    default:
      return assertNever(failure)
  }
}
