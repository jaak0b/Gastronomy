import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { PrinterStatusRow } from '../../src/core/apiTypes'

vi.mock('@microsoft/signalr', async () => (await import('./../support/hubConnection')).signalrModuleFake())

const { navigate } = await import('../../src/router')
const { useConnectionStore } = await import('../../src/stores/connection')
const { usePrinterStatusStore } = await import('../../src/stores/printerStatus')
const { TOKEN_STORAGE_KEY } = await import('../../src/stores/session')
const App = (await import('../../src/App.vue')).default
const { testPlugins } = await import('../support/plugins')

const HEALTHY_KITCHEN: PrinterStatusRow = {
  stationId: 'station-kueche',
  name: 'Küche',
  isOnline: true,
  isPaperEnd: false,
  isPaperNearEnd: false,
  isCoverOpen: false,
  isFaulty: false,
  lastChangedAtUtc: '2026-09-05T18:00:00Z',
}

function stubTheLaptop(): void {
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
}

describe('where a notice sits on the screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stubTheLaptop()
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

describe('what the ordering screen says about a printer that needs attention', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stubTheLaptop()
  })

  async function orderingScreenWith(station: PrinterStatusRow): Promise<string[]> {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-here')
    navigate('/')
    const app = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()
    const printerStatus = usePrinterStatusStore()
    printerStatus.stations = [station]
    await app.vm.$nextTick()

    return app
      .findAll('.v-alert')
      .map((alert) => alert.text())
      .filter((text) => text.includes(station.name))
  }

  it('tells the server once that the paper ran out, and that the slip is not lost', async () => {
    const messages = await orderingScreenWith({ ...HEALTHY_KITCHEN, isPaperEnd: true })

    expect(messages).toEqual([
      'Nehmen Sie weiter Bestellungen auf. Der Drucker an der Ausgabestelle Küche hat kein Papier, und der Bon wird gedruckt, sobald jemand eine Rolle einlegt.',
    ])
  })

  it('tells the server once that the station is not answering', async () => {
    const messages = await orderingScreenWith({ ...HEALTHY_KITCHEN, isOnline: false })

    expect(messages).toEqual([
      'Nehmen Sie weiter Bestellungen auf. Die Ausgabestelle Küche antwortet gerade nicht, und der Bon wird gedruckt, sobald sie wieder antwortet.',
    ])
  })

  it('tells the server once to pass the orders on by mouth while the printer is broken', async () => {
    const messages = await orderingScreenWith({ ...HEALTHY_KITCHEN, isFaulty: true })

    expect(messages).toEqual([
      'Sagen Sie jede Bestellung an der Ausgabestelle Küche persönlich an. Der Drucker dort nimmt nichts mehr an.',
    ])
  })
})
