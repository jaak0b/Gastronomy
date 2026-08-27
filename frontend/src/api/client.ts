import { isApiErrorBody, type ApiErrorBody } from '../core/apiError'

export type ApiResult<T> =
  | { kind: 'ok'; status: number; data: T }
  | { kind: 'error'; status: number; body: ApiErrorBody | null }
  | { kind: 'unreachable' }

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT'
  body?: unknown
  token?: string | null
}

export async function request<T>(path: string, options: RequestOptions = {}): Promise<ApiResult<T>> {
  const headers: Record<string, string> = {}
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (options.token !== undefined && options.token !== null) {
    headers['Authorization'] = `Bearer ${options.token}`
  }
  let response: Response
  try {
    response = await fetch(path, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
    })
  } catch {
    return { kind: 'unreachable' }
  }
  if (response.status === 204) {
    return { kind: 'ok', status: response.status, data: undefined as T }
  }
  let payload: unknown = null
  try {
    payload = await response.json()
  } catch {
    payload = null
  }
  if (!response.ok) {
    return {
      kind: 'error',
      status: response.status,
      body: isApiErrorBody(payload) ? payload : null,
    }
  }
  return { kind: 'ok', status: response.status, data: payload as T }
}
