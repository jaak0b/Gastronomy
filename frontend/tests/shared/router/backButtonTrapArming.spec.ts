import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

describe('the app puts a device behind the door as soon as its screen is on display', () => {
  beforeEach(() => {
    vi.resetModules()
    setActivePinia(createPinia())
    localStorage.clear()
    sessionStorage.clear()
    sessionStorage.setItem('theDoorAnchor', 'yes')
    window.history.replaceState({}, '', '/the-page-the-device-came-from')
    window.history.pushState({}, '', '/stations')
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        const payload = url.startsWith('/api/session')
          ? {
              deviceId: 'device-1',
              staffMember: null,
              station: { id: 's-1', name: 'Küche' },
              language: 'de',
            }
          : {
              festival: null,
              stations: [],
              stationOrders: [],
              orders: [],
              asItComes: [],
              items: [],
              station: { id: 's-1', name: 'Küche' },
            }
        return new Response(JSON.stringify(payload), { status: 200 })
      }),
    )
  })

  it('stops growing the history once the station screen is on display', async () => {
    const { useSessionStore } = await import('../../../src/shared/stores/session')
    useSessionStore().deviceToken = 'a-token'

    const app = await (await import('../../support/mountApp')).mountApp()
    await vi.waitFor(() => expect(app.find('.station-page').exists()).toBe(true))

    const { navigate } = await import('../../../src/shared/router/router')
    const { BACK_BUTTON_TRAP_RETURN_PATH_KEY } = await import('../../../src/shared/router/backButtonTrap')
    const entries = window.history.length

    navigate('/open-items')

    expect(window.history.length).toBe(entries)
    expect(sessionStorage.getItem(BACK_BUTTON_TRAP_RETURN_PATH_KEY)).toBe('/')
  })

  it('asks for the start button on a tab where the doors were never opened', async () => {
    sessionStorage.clear()
    const { useSessionStore } = await import('../../../src/shared/stores/session')
    useSessionStore().deviceToken = 'a-token'

    const app = await (await import('../../support/mountApp')).mountApp()
    await app.vm.$nextTick()

    expect(app.find('.door-gate').exists()).toBe(true)
    expect(app.find('.station-page').exists()).toBe(false)
  })
})
