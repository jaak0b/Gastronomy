import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../src/stores/session'

vi.mock('../../src/api/client', () => ({
  request: vi.fn(async () => ({ kind: 'error', status: 401, body: null, raw: null })),
  onUnauthorisedAnswer: vi.fn(),
  answerSaysTheDeviceIsNoLongerSetUp: vi.fn(() => true),
}))

describe('a device the laptop refuses while it starts, with its token still in place', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('offers to ask the laptop again instead of waiting for an answer that will not come', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-the-laptop-issued')
    const session = useSessionStore()

    await session.loadSession()

    expect(session.startingUpFailure).toBe('theLaptopCouldNotAnswer')
  })
})
