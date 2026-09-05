import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { testPlugins } from '../../support/plugins'
import type { StationScreenOrderRow } from '../../../src/core/apiTypes'

const registeredHandlers: { eventName: string; handler: (payload: unknown) => void }[] = []

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
        on: (eventName: string, handler: (payload: unknown) => void) => {
          registeredHandlers.push({ eventName, handler })
        },
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

const StationPage = (await import('../../../src/views/StationPage.vue')).default
const { useStationStore } = await import('../../../src/stores/station')
const { useConnectionStore } = await import('../../../src/stores/connection')

const SLIP: StationScreenOrderRow = {
  stationOrderId: 'ticket-1',
  orderId: 'order-1',
  globalOrderNumber: 137,
  stationOrderNumber: 42,
  tableName: 'Tisch 12',
  orderCreatedAtUtc: '2026-08-27T19:00:00Z',
  status: 'Queued',
  canHandleOnPaper: false,
  copyNumber: 0,
  orderNote: null,
  items: [{ quantity: 2, itemName: 'Bratwurst', itemNote: null }],
}

const laptop = {
  slipsAreReachable: true,
  printerIsReachable: true,
  slips: [] as StationScreenOrderRow[],
}
const requestedUrls: string[] = []

function stubLaptop(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      requestedUrls.push(url)
      if (url === '/api/stations') {
        return new Response(
          JSON.stringify({
            stations: [{ stationId: 'station-kueche', name: 'Küche', canPrint: true }],
          }),
          { status: 200 },
        )
      }
      if (url.includes('/station-orders')) {
        return laptop.slipsAreReachable
          ? new Response(JSON.stringify({ stationOrders: laptop.slips }), { status: 200 })
          : new Response('{}', { status: 500 })
      }
      return laptop.printerIsReachable
        ? new Response(JSON.stringify({ stationId: 'station-kueche', name: 'Küche' }), {
            status: 200,
          })
        : new Response('{}', { status: 500 })
    }),
  )
}

async function mountPage() {
  const view = mount(StationPage, { global: { plugins: testPlugins() } })
  await flushPromises()
  return view
}

function freshScreen(): void {
  setActivePinia(createPinia())
  localStorage.clear()
  registeredHandlers.length = 0
  requestedUrls.length = 0
  laptop.slipsAreReachable = true
  laptop.printerIsReachable = true
  laptop.slips = []
  stubLaptop()
}

describe('the station screen and the laptop pushing news', () => {
  beforeEach(freshScreen)

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetches its slips again when a slip has finished printing', async () => {
    await mountPage()
    const connection = useConnectionStore()
    await connection.connect({ deviceToken: 'a-token' })
    requestedUrls.length = 0

    for (const entry of registeredHandlers.filter(
      (candidate) => candidate.eventName === 'PrintJobStatusChanged',
    )) {
      entry.handler({})
    }
    await flushPromises()

    expect(requestedUrls.some((url) => url.includes('/station-orders'))).toBe(true)
  })
})

describe('a station screen that could not reach the laptop', () => {
  beforeEach(freshScreen)

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says the slips on screen may be out of date', async () => {
    laptop.slipsAreReachable = false

    const view = await mountPage()

    expect(view.get('.station-load-failed').text()).toBe(
      'Laden Sie die Seite neu. Der Laptop war nicht erreichbar, deshalb kann diese Liste veraltet sein.',
    )
  })

  it('keeps the slips it already has on screen, because staff are working them off', async () => {
    laptop.slips = [SLIP]
    const view = await mountPage()
    const station = useStationStore()

    laptop.slipsAreReachable = false
    await station.refresh()
    await flushPromises()

    expect(view.findAll('.station-stationOrder-row')).toHaveLength(1)
  })

  it('does not claim the pile is empty while the list could not be fetched', async () => {
    laptop.slipsAreReachable = false

    const view = await mountPage()

    expect(view.find('.empty').exists()).toBe(false)
  })

  it('does not claim the station has no printer while the printer status could not be fetched', async () => {
    laptop.printerIsReachable = false

    const view = await mountPage()

    expect(view.find('.no-printer').exists()).toBe(false)
  })
})
