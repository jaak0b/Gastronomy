import type { z } from 'zod'
import { isApiErrorBody, type ApiErrorBody } from '../core/apiError'

export type ApiResult<T> =
  | { kind: 'ok'; status: number; data: T }
  | { kind: 'error'; status: number; body: ApiErrorBody | null; raw: unknown }
  | { kind: 'unreachable' }
  | { kind: 'unreadableAnswer'; status: number; raw: unknown }

export interface RequestOptions {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  body?: unknown
  token?: string | null
  timeoutMs?: number
}

const UNAUTHORISED = 401

let reportThatTheDeviceIsNoLongerKnown: () => void = () => undefined

export function onUnauthorisedAnswer(report: () => void): void {
  reportThatTheDeviceIsNoLongerKnown = report
}

export function answerSaysTheDeviceIsNoLongerSetUp(result: ApiResult<unknown>): boolean {
  return result.kind === 'error' && result.status === UNAUTHORISED
}

export function answerIsABusinessRefusal(result: ApiResult<unknown>): boolean {
  return (
    result.kind === 'error'
    && (result.status === 400 || result.status === 422)
    && result.body !== null
  )
}

type RequestOutcome = { kind: 'notReached' } | { kind: 'answered'; response: Response }

async function sendTheRequest(path: string, options: RequestOptions): Promise<RequestOutcome> {
  const headers: Record<string, string> = {}
  if (options.body !== undefined) {
    headers['Content-Type'] = 'application/json'
  }
  if (options.token !== undefined && options.token !== null) {
    headers['Authorization'] = `Bearer ${options.token}`
  }
  const timeLimit = options.timeoutMs === undefined ? null : new AbortController()
  const alarm =
    timeLimit === null ? null : setTimeout(() => timeLimit.abort(), options.timeoutMs)
  let response: Response
  try {
    response = await fetch(path, {
      method: options.method ?? 'GET',
      headers,
      body: options.body === undefined ? undefined : JSON.stringify(options.body),
      signal: timeLimit?.signal,
    })
  } catch {
    return { kind: 'notReached' }
  } finally {
    if (alarm !== null) {
      clearTimeout(alarm)
    }
  }
  if (response.status === UNAUTHORISED) {
    reportThatTheDeviceIsNoLongerKnown()
  }
  return { kind: 'answered', response }
}

async function readJsonBody(response: Response): Promise<unknown> {
  try {
    return await response.json()
  } catch {
    return null
  }
}

function errorAnswer(status: number, payload: unknown): ApiResult<never> {
  return {
    kind: 'error',
    status,
    body: isApiErrorBody(payload) ? payload : null,
    raw: payload,
  }
}

export async function request<T>(
  path: string,
  options: RequestOptions & { schema: z.ZodType<T> },
): Promise<ApiResult<T>> {
  const outcome = await sendTheRequest(path, options)
  if (outcome.kind === 'notReached') {
    return { kind: 'unreachable' }
  }
  const { response } = outcome
  const payload = await readJsonBody(response)
  if (!response.ok) {
    return errorAnswer(response.status, payload)
  }
  const parsed = options.schema.safeParse(payload)
  if (!parsed.success) {
    return { kind: 'unreadableAnswer', status: response.status, raw: payload }
  }
  return { kind: 'ok', status: response.status, data: parsed.data }
}

export async function requestAction(
  path: string,
  options: RequestOptions = {},
): Promise<ApiResult<null>> {
  const outcome = await sendTheRequest(path, options)
  if (outcome.kind === 'notReached') {
    return { kind: 'unreachable' }
  }
  const { response } = outcome
  if (response.ok) {
    return { kind: 'ok', status: response.status, data: null }
  }
  return errorAnswer(response.status, await readJsonBody(response))
}
