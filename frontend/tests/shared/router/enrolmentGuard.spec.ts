import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { stubLaptop, answer } from '../../support/laptop'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const { currentRoute } = await import('../../../src/shared/router/router')
const { mountApp } = await import('../../support/mountApp')

describe('a phone that is not enrolled', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    sessionStorage.setItem('theDoorAnchor', 'yes')
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('is sent back to the welcome screen when it opens the review screen', async () => {
    currentRoute.value = { name: 'review' }

    const app = await mountApp()
    await vi.waitFor(() => expect(app.html().length).toBeGreaterThan(0))

    expect(app.find('[data-test="welcome"]').exists()).toBe(true)
  })
})
