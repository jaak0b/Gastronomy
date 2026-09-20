import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const { currentRoute } = await import('../../../src/shared/router/router')
const { mountApp } = await import('../../support/mountApp')

describe('a phone that is not enrolled', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    sessionStorage.setItem('theDoorAnchor', 'yes')
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('is sent back to the welcome screen when it opens the review screen', async () => {
    currentRoute.value = { name: 'review' }

    const app = await mountApp()
    await vi.waitFor(() => expect(app.html().length).toBeGreaterThan(0))

    expect(app.find('.welcome').exists()).toBe(true)
  })
})
