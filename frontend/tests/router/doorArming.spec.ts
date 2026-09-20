import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'

vi.mock('@microsoft/signalr', async () => (await import('../support/hubConnection')).signalrModuleFake())

async function mountTheApp() {
  const App = (await import('../../src/App.vue')).default
  const de = (await import('../../src/locales/de.json')).default
  const en = (await import('../../src/locales/en.json')).default
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(App, { global: { plugins: [i18n] } })
}

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
              deviceKind: 'station',
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
    const { useSessionStore } = await import('../../src/stores/session')
    useSessionStore().deviceToken = 'a-token'

    const app = await mountTheApp()
    await vi.waitFor(() => expect(app.find('.station-page').exists()).toBe(true))

    const { navigate, THE_DOOR_TARGET_KEY } = await import('../../src/router')
    const entries = window.history.length

    navigate('/open-items')

    expect(window.history.length).toBe(entries)
    expect(sessionStorage.getItem(THE_DOOR_TARGET_KEY)).toBe('/')
  })

  it('asks for the start button on a tab where the doors were never opened', async () => {
    sessionStorage.clear()
    const { useSessionStore } = await import('../../src/stores/session')
    useSessionStore().deviceToken = 'a-token'

    const app = await mountTheApp()
    await app.vm.$nextTick()

    expect(app.find('.door-gate').exists()).toBe(true)
    expect(app.find('.station-page').exists()).toBe(false)
  })
})
