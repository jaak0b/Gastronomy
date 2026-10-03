import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { stubLaptop, answer } from './support/laptop'
import { nextTick } from 'vue'

vi.mock('@microsoft/signalr', async () => (await import('./support/hubConnection')).signalrModuleFake())

const { navigate } = await import('../src/shared/router/router')
const { useConnectionStore } = await import('../src/shared/stores/connection')
const { TOKEN_STORAGE_KEY } = await import('../src/shared/stores/session')
const App = (await import('../src/App.vue')).default
const { testPlugins } = await import('./support/plugins')

function phoneLaptop(): void {
  stubLaptop()
    .answersEverythingElse(
      answer({
        deviceId: 'device-1',
        staffMember: { id: 'staff-1', name: 'Anna' },
        station: null,
        language: 'de',
      }),
    )
    .answers('GET', /^\/api\/open-items/, answer({ tables: [], itemsWithoutAnOrderCount: 0 }))
    .answers('GET', /^\/api\/open-items\/table-names/, answer({ tableNames: [] }))
    .answers('GET', /^\/api\/estimates/, answer([]))
    .answers('GET', /^\/api\/catalog/, answer({ festival: null, categories: [], items: [], stations: [] }))
}

describe('where a notice sits on the screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    sessionStorage.setItem('theDoorAnchor', 'yes')
    document.body.innerHTML = ''
    phoneLaptop()
  })

  it('stands in the page rather than behind the fixed row of buttons', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    navigate('/')
    const app = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()
    const connection = useConnectionStore()
    connection.state = 'offline'
    await nextTick()

    const notice = app.get('[data-test="connection"]').element

    expect(notice.closest('[data-test="main"]')).not.toBeNull()
  })
})

describe('where the app bar stands', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    sessionStorage.setItem('theDoorAnchor', 'yes')
    document.body.innerHTML = ''
    phoneLaptop()
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

    expect(app.find('[data-test="app-header"]').exists()).toBe(true)
  })

  it('keeps the bar over the open items', async () => {
    const app = await mountAppAt('/open-items')

    expect(app.find('[data-test="app-header"]').exists()).toBe(true)
  })

  it('leaves the bar off the review, and still shows the notices there', async () => {
    const app = await mountAppAt('/review')
    const connection = useConnectionStore()
    connection.state = 'offline'
    await nextTick()

    expect(app.find('[data-test="app-header"]').exists()).toBe(false)
    expect(app.get('[data-test="connection"]').exists()).toBe(true)
  })
})
