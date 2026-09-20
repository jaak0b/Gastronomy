import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@microsoft/signalr', async () => (await import('./support/hubConnection')).signalrModuleFake())

const { navigate } = await import('../src/shared/router/router')
const App = (await import('../src/App.vue')).default
const { testPlugins } = await import('./support/plugins')
const { DRAFT_STORAGE_KEY, SEND_PROGRESS_STORAGE_KEY } = await import('../src/phone/core/draftCart')
const { LANGUAGE_STORAGE_KEY, TOKEN_STORAGE_KEY } = await import('../src/shared/stores/session')
const { useOrderStore } = await import('../src/phone/stores/order')

let device: ReturnType<typeof mount> | null = null

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  sessionStorage.clear()
  sessionStorage.setItem('theDoorAnchor', 'yes')
  document.body.innerHTML = ''
})

afterEach(() => {
  device?.unmount()
  device = null
  vi.unstubAllGlobals()
})

describe('a phone carrying values an older build left in its storage', () => {
  it('shows the enrolment screen, starts on an empty order and asks the laptop nothing', async () => {
    const requests: string[] = []
    localStorage.setItem(DRAFT_STORAGE_KEY, '{"tableName":"Tisch 12","lines":[]}')
    localStorage.setItem(
      SEND_PROGRESS_STORAGE_KEY,
      JSON.stringify({
        state: 'failed',
        attempts: 1,
        failure: null,
        unresolvedAttempt: null,
        answeredAtUtc: '2026-01-01T00:00:00Z',
      }),
    )
    localStorage.setItem(TOKEN_STORAGE_KEY, '{"deviceToken":"from-an-old-build"}')
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'fr')
    sessionStorage.setItem('theDoorTarget', '{"route":"/review"}')
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        requests.push(url)
        throw new TypeError('the phone must not ask the laptop anything')
      }),
    )

    const order = useOrderStore()
    navigate('/')
    device = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()

    expect(device.find('.welcome').exists()).toBe(true)
    expect(order.draft.tableName).toBe('')
    expect(order.draft.lines).toEqual([])
    expect(order.sendState).toBe('idle')
    expect(order.changesAreRefused).toBe(false)
    expect(requests).toEqual([])
  })
})
