import type { AdminErrorMessage } from './adminErrorMessage'
import { assertNever } from '../shared/core/assertNever'

export type AdminActionResult<T = null> =
  | { kind: 'ok'; value: T }
  | { kind: 'failed'; message: AdminErrorMessage }

export function adminOk<T>(value: T): AdminActionResult<T> {
  return { kind: 'ok', value }
}

export function adminFailed(message: AdminErrorMessage): AdminActionResult<never> {
  return { kind: 'failed', message }
}

export function refusalFrom(result: AdminActionResult<unknown>): AdminErrorMessage | null {
  switch (result.kind) {
    case 'ok':
      return null
    case 'failed':
      return result.message
    default:
      assertNever(result)
  }
}
