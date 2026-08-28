import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@microsoft/signalr', async () => (await import('./../support/hubConnection')).signalrModuleFake())

const { navigate } = await import('../../src/router')
const { useConnectionStore } = await import('../../src/stores/connection')
const { TOKEN_STORAGE_KEY } = await import('../../src/stores/session')
const App = (await import('../../src/App.vue')).default
const { testPlugins } = await import('../support/plugins')

describe('where a notice sits on the screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(
            JSON.stringify({
              language: 'de',
              version: '',
              categories: [],
              items: [],
              stations: [],
              tableSuggestions: [],
              orders: [],
              printers: [],
            }),
            { status: 200 },
          ),
      ),
    )
  })

  it('stands in the page rather than behind the fixed row of buttons', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-here')
    navigate('/')
    const app = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
    const connection = useConnectionStore()
    connection.state = 'offline'
    await app.vm.$nextTick()

    const notice = app.get('.connection').element

    expect(notice.closest('.v-main')).not.toBeNull()
  })
})
