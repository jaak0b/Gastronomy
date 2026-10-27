import { request } from '../../shared/api/client'
import { OrderItemHistoryView } from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'

export type ItemHistoryAnswer =
  | { kind: 'found'; history: OrderItemHistoryView }
  | { kind: 'refused'; failureKey: string }
  | { kind: 'unanswered' }

export async function readTheItemHistory(orderItemId: string, token: string): Promise<ItemHistoryAnswer> {
  const result = await request(`/api/open-items/items/${encodeURIComponent(orderItemId)}/history`, {
    token,
    schema: OrderItemHistoryView,
  })
  switch (result.kind) {
    case 'ok':
      return { kind: 'found', history: result.data }
    case 'error':
      return result.body === null ? { kind: 'unanswered' } : { kind: 'refused', failureKey: result.body.messageKey }
    case 'unreachable':
    case 'unreadableAnswer':
      return { kind: 'unanswered' }
    default:
      return assertNever(result)
  }
}
