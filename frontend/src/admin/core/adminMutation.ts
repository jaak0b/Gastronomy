import type { ApiResult } from '../../shared/api/client'
import { adminErrorMessage } from './adminErrorMessage'
import { adminFailed, adminOk, type AdminActionResult } from './adminActionResult'

export function adminFailureFrom(result: ApiResult<unknown>): AdminActionResult<never> {
  return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
}

export async function reloadOrFailureOf<T>(
  result: ApiResult<T>,
  reload: () => Promise<void>,
): Promise<AdminActionResult<T>> {
  if (result.kind !== 'ok') {
    return adminFailureFrom(result)
  }
  await reload()
  return adminOk(result.data)
}
