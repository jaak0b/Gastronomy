import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import { createVuetify } from 'vuetify'

vi.mock('@microsoft/signalr', async () => (await import('../support/hubConnection')).signalrModuleFake())

const { currentRoute, resolveRoute } = await import('../../src/router')
const App = (await import('../../src/App.vue')).default
const de = (await import('../../src/locales/de.json')).default
const en = (await import('../../src/locales/en.json')).default

function mountApp() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(App, { global: { plugins: [i18n, createVuetify()] } })
}

describe('resolveRoute', () => {
  it('reads the bare admin path as the admin', () => {
    expect(resolveRoute('/admin')).toEqual({ name: 'admin', section: 'overview' })
  })

  it('reads a deep admin path as that admin section', () => {
    expect(resolveRoute('/admin/items')).toEqual({ name: 'admin', section: 'items' })
  })

  it('reads the stations path as the station screen', () => {
    expect(resolveRoute('/stations')).toEqual({ name: 'stations' })
  })

  it('no longer honours an old station link that carried a key', () => {
    expect(resolveRoute('/station/abc123')).toEqual({ name: 'home' })
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
          new Response(JSON.stringify({ stations: [], items: [], staffMembers: [] }), {
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

describe('the screen a device lands on', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        const payload = url.startsWith('/api/session')
          ? {
              deviceId: 'device-1',
              deviceKind: 'staffMember',
              staffMember: { id: 'staff-member-1', name: 'Anna' },
              station: null,
              language: 'de',
            }
          : { stations: [], slices: [], orders: [], items: [], station: { id: 's-1', name: 'Küche' } }
        return new Response(JSON.stringify(payload), { status: 200 })
      }),
    )
    currentRoute.value = { name: 'stations' }
  })

  it('keeps a station tablet on the station screen', async () => {
    const { useSessionStore } = await import('../../src/stores/session')
    const session = useSessionStore()
    session.deviceToken = 'a-token'
    session.deviceKind = 'station'

    const app = mountApp()

    await vi.waitFor(() => expect(app.find('.station-page').exists()).toBe(true))
  })

  it('sends a waiter phone that opens the station address back to the item list', async () => {
    const { useSessionStore } = await import('../../src/stores/session')
    const session = useSessionStore()
    session.deviceToken = 'a-token'
    session.deviceKind = 'staffMember'

    const app = mountApp()

    await vi.waitFor(() => expect(app.find('.catalog').exists()).toBe(true))

    expect(app.find('.station-page').exists()).toBe(false)
  })

  it('sends a device that is not set up to the welcome screen instead', async () => {
    const { useSessionStore } = await import('../../src/stores/session')
    useSessionStore().deviceToken = null

    const app = mountApp()

    await vi.waitFor(() => expect(app.find('.welcome').exists()).toBe(true))

    expect(app.find('.station-page').exists()).toBe(false)
  })
})
