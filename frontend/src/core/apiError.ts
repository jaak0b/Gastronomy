export interface ApiErrorBody {
  code: string
  messageKey: string
  parameters: Record<string, string | number>
  details: string | null
}

export function isApiErrorBody(value: unknown): value is ApiErrorBody {
  if (typeof value !== 'object' || value === null) {
    return false
  }
  const candidate = value as Record<string, unknown>
  return typeof candidate.code === 'string' && typeof candidate.messageKey === 'string'
}
