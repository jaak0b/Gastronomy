import { beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const { currentRoute, resolveRoute } = await import('../../../src/shared/router/router')
const { mountApp } = await import('../../support/mountApp')

describe('resolveRoute', () => {
  it('reads the bare admin path as the festivals, where the admin starts', () => {
    expect(resolveRoute('/admin')).toEqual({
      name: 'admin',
      section: 'festivals',
      festivalId: null,
    })
  })

  it('reads a deep admin path as that admin section', () => {
    expect(resolveRoute('/admin/items')).toEqual({
      name: 'admin',
      section: 'items',
      festivalId: null,
    })
  })

  it('reads the address of one festival as that festival page', () => {
    expect(resolveRoute('/admin/festivals/fest-1')).toEqual({
      name: 'admin',
      section: 'festivals',
      festivalId: 'fest-1',
    })
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
    sessionStorage.setItem('theDoorAnchor', 'yes')
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify({ festivals: [], stations: [], items: [], staffMembers: [] }), {
            status: 200,
          }),
      ),
    )
    currentRoute.value = { name: 'admin', section: 'festivals', festivalId: null }
    window.history.replaceState({}, '', '/admin/festivals')
  })

  it('renders the admin shell without a device token', async () => {
    const app = await mountApp()
    await vi.waitFor(() => expect(app.find('.admin-shell').exists()).toBe(true))
  })

  it('never sends the laptop to the enrolment screen', async () => {
    const app = await mountApp()
    await vi.waitFor(() => expect(app.find('.admin-shell').exists()).toBe(true))

    expect(app.find('.welcome').exists()).toBe(false)
    expect(app.find('.code-field').exists()).toBe(false)
  })

  it('mounts no phone header on the admin', async () => {
    const app = await mountApp()
    await vi.waitFor(() => expect(app.find('.admin-shell').exists()).toBe(true))

    expect(app.find('.app-header').exists()).toBe(false)
  })
})

describe('the screen a device lands on', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    sessionStorage.setItem('theDoorAnchor', 'yes')
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        const payload = url.startsWith('/api/session')
          ? {
              deviceId: 'device-1',
              staffMember: { id: 'staff-member-1', name: 'Anna' },
              station: null,
              language: 'de',
            }
          : {
              festival: null,
              stations: [],
              stationOrders: [],
              orders: [],
              items: [],
              station: { id: 's-1', name: 'Küche' },
            }
        return new Response(JSON.stringify(payload), { status: 200 })
      }),
    )
    currentRoute.value = { name: 'stations' }
  })

  it('keeps a station tablet on the station screen', async () => {
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
              items: [],
              station: { id: 's-1', name: 'Küche' },
            }
        return new Response(JSON.stringify(payload), { status: 200 })
      }),
    )
    const { useSessionStore } = await import('../../../src/shared/stores/session')
    useSessionStore().deviceToken = 'a-token'

    const app = await mountApp()

    await vi.waitFor(() => expect(app.find('.station-page').exists()).toBe(true))
  })

  it('sends a waiter phone that opens the station address back to the item list', async () => {
    const { useSessionStore } = await import('../../../src/shared/stores/session')
    const session = useSessionStore()
    session.deviceToken = 'a-token'
    session.staffMember = { id: 'staff-1', name: 'Anna' }

    const app = await mountApp()

    await vi.waitFor(() => expect(app.find('.catalog').exists()).toBe(true))

    expect(app.find('.station-page').exists()).toBe(false)
  })

  it('sends a device that is not set up to the welcome screen instead', async () => {
    const { useSessionStore } = await import('../../../src/shared/stores/session')
    useSessionStore().deviceToken = null

    const app = await mountApp()

    await vi.waitFor(() => expect(app.find('.welcome').exists()).toBe(true))

    expect(app.find('.station-page').exists()).toBe(false)
  })
})
