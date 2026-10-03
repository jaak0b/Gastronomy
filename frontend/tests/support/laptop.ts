import { vi } from 'vitest'

export interface LaptopCall {
  url: string
  method: string
  body: unknown
}

export type LaptopReply = (call: LaptopCall, signal?: AbortSignal) => Response | Promise<Response>

export type RequestMatch = string | RegExp | ((call: LaptopCall) => boolean)

export type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'ANY'

interface Route {
  method: HttpMethod
  match: RequestMatch
  reply: LaptopReply
}

export interface StubbedLaptop {
  readonly calls: LaptopCall[]
  answers(method: HttpMethod, match: RequestMatch, reply: LaptopReply): StubbedLaptop
  answersEverythingElse(reply: LaptopReply): StubbedLaptop
  writes(): LaptopCall[]
  writtenBodies(): unknown[]
  callsTo(method: HttpMethod, match: RequestMatch): LaptopCall[]
  urls(): string[]
}

export function stubLaptop(): StubbedLaptop {
  const calls: LaptopCall[] = []
  const routes: Route[] = []
  let fallback: LaptopReply | null = null
  const laptop: StubbedLaptop = {
    calls,
    answers(method, match, reply) {
      routes.unshift({ method, match, reply })
      return laptop
    },
    answersEverythingElse(reply) {
      fallback = reply
      return laptop
    },
    writes() {
      return calls.filter((call) => call.method !== 'GET')
    },
    writtenBodies() {
      return laptop.writes().map((call) => call.body)
    },
    callsTo(method, match) {
      return calls.filter((call) => isMatching({ method, match }, call))
    },
    urls() {
      return calls.map((call) => call.url)
    },
  }
  vi.stubGlobal(
    'fetch',
    vi.fn((url: string, init?: RequestInit): Promise<Response> => {
      const call: LaptopCall = { url, method: init?.method ?? 'GET', body: bodyOf(init) }
      calls.push(call)
      const route = routes.find((candidate) => isMatching(candidate, call))
      const reply = route?.reply ?? fallback
      if (reply === null) {
        return Promise.reject(
          new TypeError(`the stubbed laptop has no answer for ${call.method} ${call.url}`),
        )
      }
      return Promise.resolve(reply(call, init?.signal ?? undefined))
    }),
  )
  return laptop
}

export function answer(body: unknown, status = 200): LaptopReply {
  return () => new Response(JSON.stringify(body), { status })
}

export function emptyAnswer(status: number): LaptopReply {
  return () => new Response(null, { status })
}

export function refusal(
  messageKey: string,
  { status = 409, code = 'Conflict', parameters = {} as Record<string, unknown> } = {},
): LaptopReply {
  return answer({ code, messageKey, parameters, details: null }, status)
}

export function inTurn(...replies: LaptopReply[]): LaptopReply {
  let position = 0
  return (call, signal) => {
    const reply = replies[Math.min(position, replies.length - 1)]
    position += 1
    return reply(call, signal)
  }
}

export function noConnection(): LaptopReply {
  return () => Promise.reject(new TypeError('Failed to fetch'))
}

export function heldUntil(released: Promise<unknown>, reply: LaptopReply): LaptopReply {
  return async (call, signal) => {
    await released
    return reply(call, signal)
  }
}

export interface Hold {
  released: Promise<void>
  release: () => void
}

export function aHold(): Hold {
  const hold: Hold = { released: Promise.resolve(), release: () => undefined }
  hold.released = new Promise<void>((resolve) => {
    hold.release = resolve
  })
  return hold
}

export function neverAnswers(): LaptopReply {
  return (_call, signal) =>
    new Promise<Response>((_resolve, reject) => {
      signal?.addEventListener('abort', () => {
        reject(new DOMException('The request was aborted', 'AbortError'))
      })
    })
}

function bodyOf(init?: RequestInit): unknown {
  if (init?.body === undefined) {
    return undefined
  }
  if (init.body === null) {
    return null
  }
  return typeof init.body === 'string' ? JSON.parse(init.body) : init.body
}

function isMatching(route: Pick<Route, 'method' | 'match'>, call: LaptopCall): boolean {
  if (route.method !== 'ANY' && route.method !== call.method) {
    return false
  }
  if (typeof route.match === 'function') {
    return route.match(call)
  }
  if (route.match instanceof RegExp) {
    return route.match.test(call.url)
  }
  return (
    call.url === route.match ||
    call.url.startsWith(`${route.match}/`) ||
    call.url.startsWith(`${route.match}?`)
  )
}

export function stubLaptopAnswering(
  payloadFor: (url: string, method: string) => unknown,
  writeStatus = 200,
): StubbedLaptop {
  return stubLaptop().answersEverythingElse((call) =>
    answer(payloadFor(call.url, call.method), call.method === 'GET' ? 200 : writeStatus)(call),
  )
}

export function stubLaptopAt(repliesByUrl: Record<string, LaptopReply>): StubbedLaptop {
  const laptop = stubLaptop()
  for (const [url, reply] of Object.entries(repliesByUrl)) {
    laptop.answers('ANY', (call) => call.url === url, reply)
  }
  return laptop
}
