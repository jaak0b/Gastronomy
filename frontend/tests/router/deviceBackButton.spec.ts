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

async function pressBack(): Promise<void> {
  const popped = new Promise((resolve) => {
    window.addEventListener('popstate', () => resolve(undefined), { once: true })
  })
  window.history.back()
  await popped
}

const theScreenTheDeviceCameFrom = '/the-page-the-device-came-from'

describe('the back button on a device that shows the app', () => {
  beforeEach(() => {
    vi.resetModules()
    setActivePinia(createPinia())
    localStorage.clear()
    window.history.replaceState({}, '', theScreenTheDeviceCameFrom)
  })

  it('keeps the invitation screen in place when a phone presses back', async () => {
    window.history.pushState({}, '', '/j/abc123')

    const app = await mountTheApp()
    await app.vm.$nextTick()

    await pressBack()
    await app.vm.$nextTick()

    expect(window.location.pathname).toBe('/j/abc123')
  })

  it('keeps the welcome screen in place when back is pressed', async () => {
    window.history.pushState({}, '', '/')

    const app = await mountTheApp()
    await app.vm.$nextTick()

    await pressBack()
    await app.vm.$nextTick()

    expect(window.location.pathname).toBe('/')
  })

  it('keeps a station tablet on the station screen when back is pressed', async () => {
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
              stations: [],
              slices: [],
              orders: [],
              items: [],
              station: { id: 's-1', name: 'Küche' },
            }
        return new Response(JSON.stringify(payload), { status: 200 })
      }),
    )
    const { useSessionStore } = await import('../../src/stores/session')
    useSessionStore().deviceToken = 'a-token'

    const app = await mountTheApp()
    await vi.waitFor(() => expect(app.find('.station-page').exists()).toBe(true))

    await pressBack()
    await app.vm.$nextTick()

    expect(window.location.pathname).toBe('/stations')
    expect(app.find('.station-page').exists()).toBe(true)
  })
})
