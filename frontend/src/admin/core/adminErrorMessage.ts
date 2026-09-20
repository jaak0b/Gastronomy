import de from '../../shared/i18n/de.json'
import type { ApiErrorBody } from '../../shared/api/apiError'
import type { Translate } from '../../shared/core/translation'

export interface AdminErrorMessage {
  key: string
  parameters: Record<string, string | number>
  count: number | null
}

export const GENERIC_ADMIN_ERROR_KEY = 'admin.actionFailed'

interface MessageTree {
  [key: string]: string | MessageTree
}

function knownKeys(tree: MessageTree, prefix = ''): Set<string> {
  const known = new Set<string>()
  for (const [key, value] of Object.entries(tree)) {
    const path = prefix === '' ? key : `${prefix}.${key}`
    if (typeof value === 'string') {
      known.add(path)
    } else {
      for (const nested of knownKeys(value, path)) {
        known.add(nested)
      }
    }
  }
  return known
}

const RENDERABLE_KEYS = knownKeys(de as MessageTree)

function wholeNumberOrNull(value: number): number | null {
  return Number.isInteger(value) ? value : null
}

function countFrom(parameters: Record<string, string | number>): number | null {
  const count = parameters.count
  if (typeof count === 'number') {
    return wholeNumberOrNull(count)
  }
  if (typeof count !== 'string' || count.trim() === '') {
    return null
  }
  return wholeNumberOrNull(Number(count))
}

export function adminErrorMessage(body: ApiErrorBody | null): AdminErrorMessage {
  if (body === null || !RENDERABLE_KEYS.has(body.messageKey)) {
    return { key: GENERIC_ADMIN_ERROR_KEY, parameters: {}, count: null }
  }
  const parameters = body.parameters ?? {}
  return { key: body.messageKey, parameters, count: countFrom(parameters) }
}

export function adminErrorMessageForKey(
  messageKey: string,
  parameters: Record<string, string | number> = {},
): AdminErrorMessage {
  return adminErrorMessage({ code: '', messageKey, parameters, details: null })
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

export function adminBlockingConditionMessagesFrom(value: unknown): AdminErrorMessage[] {
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
    const resolved = adminErrorMessageForKey(messageKey, parameters)
    if (listed.some((shown) => shown.key === resolved.key && resolved.key === GENERIC_ADMIN_ERROR_KEY)) {
      continue
    }
    listed.push(resolved)
  }
  return listed
}

export function adminErrorMessageText(translate: Translate, message: AdminErrorMessage): string {
  return message.count === null
    ? translate(message.key, message.parameters)
    : translate(message.key, message.parameters, message.count)
}
