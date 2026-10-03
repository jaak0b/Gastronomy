import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { stubLaptop, answer, type StubbedLaptop } from '../../support/laptop'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../../src/shared/stores/connection')
const { useCatalogStore } = await import('../../../src/phone/stores/catalog')
const { useStationStore } = await import('../../../src/station/stores/station')
const { useSessionStore } = await import('../../../src/shared/stores/session')

const KITCHEN = { id: 'station-kueche', name: 'Kueche am Zelt' }

function stationLaptop(): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(
      answer({
        festival: null,
        categories: [],
        items: [],
        stations: [],
        station: KITCHEN,
        orders: [],
        asItComes: [],
      }),
    )
    .answers('GET', (call) => call.url === '/api/station/orders/fulfilled', answer({ stationOrders: [] }))
}

async function letTheReloadFinish(): Promise<void> {
  await new Promise((resolve) => setTimeout(resolve, 0))
}

describe('a production location the admin renamed or switched off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reaches the phone, which loads the menu again to get the current list', async () => {
    const laptop = stationLaptop()
    useCatalogStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    laptop.forgetCalls()

    fireHubEvent('ConfigurationChanged')
    await letTheReloadFinish()

    expect(laptop.urls()).toEqual(['/api/catalog'])
  })

  it('reaches the tablet standing at it, which loads its board again to get the current name', async () => {
    const laptop = stationLaptop()
    useStationStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    laptop.forgetCalls()

    fireHubEvent('ConfigurationChanged')
    await letTheReloadFinish()

    expect(laptop.urls()).toEqual(['/api/station/orders'])
  })

  it('is left alone once the tablet has stopped listening', async () => {
    const laptop = stationLaptop()
    const stopListening = useStationStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    stopListening()
    laptop.forgetCalls()

    fireHubEvent('ConfigurationChanged')
    await letTheReloadFinish()

    expect(laptop.urls()).toEqual([])
  })
})

describe('a change to the orders at a station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
    useSessionStore().deviceToken = 'token-here'
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('makes the tablet at that station load its queue again', async () => {
    const laptop = stationLaptop()
    useStationStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    laptop.forgetCalls()

    fireHubEvent('OrdersChanged')
    await letTheReloadFinish()

    expect(laptop.urls()).toEqual(['/api/station/orders'])
  })

  it('loads the done list too while it is open', async () => {
    const laptop = stationLaptop()
    const station = useStationStore()
    station.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    await station.openFulfilled()
    laptop.forgetCalls()

    fireHubEvent('OrdersChanged')
    await letTheReloadFinish()

    expect(laptop.urls()).toEqual(['/api/station/orders', '/api/station/orders/fulfilled'])
  })

  it('leaves the done list alone while the queue is on screen', async () => {
    const laptop = stationLaptop()
    useStationStore().listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    laptop.forgetCalls()

    fireHubEvent('OrdersChanged')
    await letTheReloadFinish()

    expect(laptop.urls()).toEqual(['/api/station/orders'])
  })
})
