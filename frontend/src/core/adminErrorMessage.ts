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

function conditionsFrom(value: unknown): unknown[] {
  if (Array.isArray(value)) {
    return value
  }
  if (typeof value === 'object' && value !== null) {
    const inner = (value as Record<string, unknown>).blockingConditions
    if (Array.isArray(inner)) {
      return inner
    }
  }
  return []
}

export function adminBlockingConditions(value: unknown): AdminErrorMessage[] {
  const listed: AdminErrorMessage[] = []
  for (const entry of conditionsFrom(value)) {
    if (typeof entry !== 'object' || entry === null) {
      continue
    }
    const candidate = entry as Record<string, unknown>
    const messageKey = typeof candidate.messageKey === 'string' ? candidate.messageKey : ''
    const parameters =
      typeof candidate.parameters === 'object' && candidate.parameters !== null
        ? (candidate.parameters as Record<string, string | number>)
        : {}
    const resolved = adminErrorMessage({
      code: '',
      messageKey,
      parameters,
      details: null,
    })
    if (listed.some((shown) => shown.key === resolved.key && resolved.key === GENERIC_ADMIN_ERROR_KEY)) {
      continue
    }
    listed.push(resolved)
  }
  return listed
}
