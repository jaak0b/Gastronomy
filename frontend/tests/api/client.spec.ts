import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { onUnauthorisedAnswer, request } from '../../src/api/client'

function laptopThatNeverAnswers(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(
      (_path: string, init: RequestInit) =>
        new Promise<Response>((_resolve, reject) => {
          init.signal?.addEventListener('abort', () => {
            reject(new DOMException('The request was aborted', 'AbortError'))
          })
        }),
    ),
  )
}

function laptopThatAnswersAtOnce(): void {
  vi.stubGlobal('fetch', vi.fn(async () => new Response('{"ok":true}', { status: 200 })))
}

describe('a request the caller gave no time limit', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('waits for the laptop as long as the phone itself waits', async () => {
    laptopThatAnswersAtOnce()

    await request('/api/anything')

    expect((vi.mocked(fetch).mock.calls[0][1] as RequestInit).signal).toBeUndefined()
  })
})

describe('a request the caller gave a time limit', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('gives up when the laptop says nothing at all, instead of waiting for the phone to give up', async () => {
    laptopThatNeverAnswers()

    const answer = request('/api/orders', { method: 'POST', body: {}, timeoutMs: 10_000 })
    await vi.advanceTimersByTimeAsync(10_000)

    expect(await answer).toEqual({ kind: 'unreachable' })
  })

  it('holds on until the limit is actually reached', async () => {
    laptopThatNeverAnswers()
    let settled = false

    const answer = request('/api/orders', { method: 'POST', body: {}, timeoutMs: 10_000 })
    void answer.then(() => (settled = true))
    await vi.advanceTimersByTimeAsync(9_000)

    expect(settled).toBe(false)
    await vi.advanceTimersByTimeAsync(1_000)
    await answer
  })

  it('hands back the answer the laptop gave in time, unchanged', async () => {
    laptopThatAnswersAtOnce()

    const answer = await request<{ ok: boolean }>('/api/anything', { timeoutMs: 10_000 })

    expect(answer).toEqual({ kind: 'ok', status: 200, data: { ok: true } })
  })

  it('takes its alarm down once the answer is in, so nothing is left ticking on the phone', async () => {
    laptopThatAnswersAtOnce()

    await request('/api/anything', { timeoutMs: 10_000 })

    expect(vi.getTimerCount()).toBe(0)
  })
})

describe('an answer that says the laptop does not know this device', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    onUnauthorisedAnswer(() => undefined)
  })

  function laptopThatAnswers(status: number): void {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status })))
  }

  it('is reported, whichever call it came back from, so the phone can be set up again', async () => {
    laptopThatAnswers(401)
    const deviceIsNoLongerKnown = vi.fn()
    onUnauthorisedAnswer(deviceIsNoLongerKnown)

    await request('/api/orders', { method: 'POST', body: {}, token: 'token-the-laptop-forgot' })

    expect(deviceIsNoLongerKnown).toHaveBeenCalledTimes(1)
  })

  it('still reaches the caller, so the screen that asked can say what happened', async () => {
    laptopThatAnswers(401)
    onUnauthorisedAnswer(() => undefined)

    const answer = await request('/api/orders', { method: 'POST', body: {} })

    expect(answer).toEqual({ kind: 'error', status: 401, body: null, raw: {} })
  })

  it('is not reported for an answer that only refuses what the device may do', async () => {
    laptopThatAnswers(403)
    const deviceIsNoLongerKnown = vi.fn()
    onUnauthorisedAnswer(deviceIsNoLongerKnown)

    await request('/api/station/slices', { token: 'token-of-a-waiter-phone' })

    expect(deviceIsNoLongerKnown).not.toHaveBeenCalled()
  })

  it('is not reported for an answer the laptop could not save', async () => {
    laptopThatAnswers(503)
    const deviceIsNoLongerKnown = vi.fn()
    onUnauthorisedAnswer(deviceIsNoLongerKnown)

    await request('/api/orders', { method: 'POST', body: {} })

    expect(deviceIsNoLongerKnown).not.toHaveBeenCalled()
  })
})
