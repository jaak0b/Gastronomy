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

const { currentRoute, resolveRoute } = await import('../../src/router')
const App = (await import('../../src/App.vue')).default
const de = (await import('../../src/locales/de.json')).default
const en = (await import('../../src/locales/en.json')).default

function mountApp() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(App, { global: { plugins: [i18n] } })
}

describe('resolveRoute', () => {
  it('reads the bare admin path as the admin', () => {
    expect(resolveRoute('/admin')).toEqual({ name: 'admin', section: 'overview' })
  })

  it('reads a deep admin path as that admin section', () => {
    expect(resolveRoute('/admin/items')).toEqual({ name: 'admin', section: 'items' })
  })

  it('reads a station path as the station page', () => {
    expect(resolveRoute('/station/abc123')).toEqual({ name: 'station', accessKey: 'abc123' })
  })
})

describe('the admin opened on the laptop, where no phone was ever set up', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify({ locations: [], items: [], printers: [], people: [] }), {
            status: 200,
          }),
      ),
    )
    currentRoute.value = { name: 'admin', section: 'overview' }
  })

  it('renders the admin shell without a device token', async () => {
    const app = mountApp()
    await vi.waitFor(() => expect(app.find('.admin-shell').exists()).toBe(true))
  })

  it('never sends the laptop to the enrolment screen', async () => {
    const app = mountApp()
    await vi.waitFor(() => expect(app.find('.admin-shell').exists()).toBe(true))

    expect(app.find('.welcome').exists()).toBe(false)
    expect(app.find('.code-field').exists()).toBe(false)
  })

  it('mounts no phone header on the admin', async () => {
    const app = mountApp()
    await vi.waitFor(() => expect(app.find('.admin-shell').exists()).toBe(true))

    expect(app.find('.app-header').exists()).toBe(false)
  })
})

describe('the station page, which authenticates by its own access key', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify({ locations: [], tickets: [] }), { status: 200 }),
      ),
    )
    currentRoute.value = { name: 'station', accessKey: 'abc123' }
  })

  it('renders without a device token', async () => {
    const app = mountApp()
    await vi.waitFor(() => expect(app.find('.station-page').exists()).toBe(true))
  })

  it('never sends the station to the enrolment screen', async () => {
    const app = mountApp()
    await vi.waitFor(() => expect(app.find('.station-page').exists()).toBe(true))

    expect(app.find('.welcome').exists()).toBe(false)
    expect(app.find('.code-field').exists()).toBe(false)
  })
})
