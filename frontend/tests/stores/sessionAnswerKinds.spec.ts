import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../src/stores/session'

vi.mock('../../src/api/client', () => ({
  request: vi.fn(async () => ({ kind: 'anAnswerNobodyWroteAHandlerFor' })),
  onUnauthorisedAnswer: vi.fn(),
  answerSaysTheDeviceIsNoLongerSetUp: vi.fn(() => false),
}))

describe('an answer of a kind the phone has no handler for', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('is refused loudly when the phone asks the laptop whose phone it is', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-the-laptop-issued')
    const session = useSessionStore()

    await expect(session.loadSession()).rejects.toThrow()
  })

  it('is refused loudly when a fresh code is scanned', async () => {
    const session = useSessionStore()

    await expect(session.redeem({ code: '123456' })).rejects.toThrow()
  })
})
