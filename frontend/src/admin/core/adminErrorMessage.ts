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

function isMessageTree(value: unknown): value is MessageTree {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function knownKeys(tree: unknown, prefix = ''): Set<string> {
  const known = new Set<string>()
  if (!isMessageTree(tree)) {
    return known
  }
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

const RENDERABLE_KEYS = knownKeys(de)

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

export function adminErrorMessageText(translate: Translate, message: AdminErrorMessage): string {
  return message.count === null
    ? translate(message.key, message.parameters)
    : translate(message.key, message.parameters, message.count)
}
