import type { ApiErrorBody } from './apiError'

export interface AdminErrorMessage {
  key: string
  parameters: Record<string, string | number>
  count: number | null
}

export const GENERIC_ADMIN_ERROR_KEY = 'admin.loadFailed'

const RENDERABLE_KEYS = [
  'admin.locations.openTickets',
  'admin.locations.lastForItems',
  'admin.itemsWouldHaveNoStation',
  'admin.stationHasNoPrinterWorker',
  'admin.items.needsLocation',
  'admin.items.deactivateBlocked',
  'admin.event.blockedOpenTickets',
  'admin.event.blockedQuestions',
  'admin.event.blockedMock',
]

function countFrom(parameters: Record<string, string | number>): number | null {
  const count = parameters.count
  return typeof count === 'number' ? count : null
}

export function adminErrorMessage(body: ApiErrorBody | null): AdminErrorMessage {
  if (body === null || !RENDERABLE_KEYS.includes(body.messageKey)) {
    return { key: GENERIC_ADMIN_ERROR_KEY, parameters: {}, count: null }
  }
  const parameters = body.parameters ?? {}
  return { key: body.messageKey, parameters, count: countFrom(parameters) }
}
