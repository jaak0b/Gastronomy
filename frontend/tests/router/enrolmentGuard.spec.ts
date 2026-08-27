import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'

vi.mock('@microsoft/signalr', () => {
  class HubConnectionBuilder {
    withUrl() {
      return this
    }
    withAutomaticReconnect() {
      return this
    }
    build() {
      return {
        state: 'Disconnected',
        on: () => undefined,
        off: () => undefined,
        onreconnecting: () => undefined,
        onreconnected: () => undefined,
        onclose: () => undefined,
        start: async () => undefined,
        stop: async () => undefined,
      }
    }
  }
  return { HubConnectionBuilder, HubConnectionState: { Disconnected: 'Disconnected' } }
})

const { currentRoute } = await import('../../src/router')
const App = (await import('../../src/App.vue')).default
const de = (await import('../../src/locales/de.json')).default
const en = (await import('../../src/locales/en.json')).default

function mountApp() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(App, { global: { plugins: [i18n] } })
}

describe('a phone that is not enrolled', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('is sent back to the welcome screen when it opens the review screen', async () => {
    currentRoute.value = { name: 'review' }

    const app = mountApp()
    await vi.waitFor(() => expect(app.html().length).toBeGreaterThan(0))

    expect(app.find('.welcome').exists()).toBe(true)
  })

  it('is sent back to the welcome screen when it opens the order list', async () => {
    currentRoute.value = { name: 'orders' }

    const app = mountApp()
    await vi.waitFor(() => expect(app.html().length).toBeGreaterThan(0))

    expect(app.find('.welcome').exists()).toBe(true)
  })

  it('is sent back to the welcome screen when it opens a single order', async () => {
    currentRoute.value = { name: 'orderDetail', orderId: 'order-1' }

    const app = mountApp()
    await vi.waitFor(() => expect(app.html().length).toBeGreaterThan(0))

    expect(app.find('.welcome').exists()).toBe(true)
  })
})
