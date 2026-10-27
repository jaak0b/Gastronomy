import { request } from '../../shared/api/client'
import { TableOrderReportView, type TableOrderSearchRequest } from '../../shared/api/generatedSchemas'
import { assertNever } from '../../shared/core/assertNever'

const TABLE_NOT_FOUND = 404

export type TableSearchAnswer =
  | { kind: 'found'; report: TableOrderReportView }
  | { kind: 'refused'; failureKey: string }
  | { kind: 'unanswered' }

export async function searchTheTable(
  tableId: string,
  options: TableOrderSearchRequest,
  token: string,
): Promise<TableSearchAnswer> {
  const result = await request(`/api/open-items/tables/${encodeURIComponent(tableId)}/search`, {
    method: 'POST',
    body: options,
    token,
    schema: TableOrderReportView,
  })
  switch (result.kind) {
    case 'ok':
      return { kind: 'found', report: result.data }
    case 'error':
      if (result.status === TABLE_NOT_FOUND) {
        return { kind: 'refused', failureKey: 'phone.openItems.errors.tableCouldNotBeLoaded' }
      }
      return result.body === null ? { kind: 'unanswered' } : { kind: 'refused', failureKey: result.body.messageKey }
    case 'unreachable':
    case 'unreadableAnswer':
      return { kind: 'unanswered' }
    default:
      return assertNever(result)
  }
}
