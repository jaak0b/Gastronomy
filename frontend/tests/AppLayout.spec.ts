import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@microsoft/signalr', async () => (await import('./support/hubConnection')).signalrModuleFake())

const { navigate } = await import('../src/shared/router/router')
const { useConnectionStore } = await import('../src/shared/stores/connection')
const { TOKEN_STORAGE_KEY } = await import('../src/shared/stores/session')
const App = (await import('../src/App.vue')).default
const { testPlugins } = await import('./support/plugins')

function answerFor(url: string): unknown {
  if (url.startsWith('/api/catalog')) {
    return { festival: null, categories: [], items: [], stations: [] }
  }
  if (url.startsWith('/api/estimates')) {
    return { stations: [] }
  }
  if (url.startsWith('/api/open-items/table-names')) {
    return { tableNames: [] }
  }
  if (url.startsWith('/api/open-items')) {
    return { tables: [], itemsWithoutAnOrderCount: 0 }
  }
  return {
    deviceId: 'device-1',
    deviceKind: 'staffMember',
    staffMember: { id: 'staff-1', name: 'Anna' },
    station: null,
    language: 'de',
  }
}

function stubTheLaptop(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => new Response(JSON.stringify(answerFor(url)), { status: 200 })),
  )
}

describe('where a notice sits on the screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    sessionStorage.setItem('theDoorAnchor', 'yes')
    document.body.innerHTML = ''
    stubTheLaptop()
  })

  it('stands in the page rather than behind the fixed row of buttons', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    navigate('/')
    const app = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()
    const connection = useConnectionStore()
    connection.state = 'offline'
    await app.vm.$nextTick()

    const notice = app.get('.connection').element

    expect(notice.closest('.v-main')).not.toBeNull()
  })
})

describe('where the app bar stands', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    sessionStorage.setItem('theDoorAnchor', 'yes')
    document.body.innerHTML = ''
    stubTheLaptop()
  })

  async function mountAppAt(path: string) {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    navigate(path)
    const app = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()
    return app
  }

  it('keeps the bar over the catalogue', async () => {
    const app = await mountAppAt('/')

    expect(app.find('.app-header').exists()).toBe(true)
  })

  it('keeps the bar over the open items', async () => {
    const app = await mountAppAt('/open-items')

    expect(app.find('.app-header').exists()).toBe(true)
  })

  it('leaves the bar off the review, and still shows the notices there', async () => {
    const app = await mountAppAt('/review')
    const connection = useConnectionStore()
    connection.state = 'offline'
    await app.vm.$nextTick()

    expect(app.find('.app-header').exists()).toBe(false)
    expect(app.get('.connection').exists()).toBe(true)
  })
})
