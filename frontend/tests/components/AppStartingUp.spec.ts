import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@microsoft/signalr', async () => (await import('./../support/hubConnection')).signalrModuleFake())

const { navigate } = await import('../../src/router')
const App = (await import('../../src/App.vue')).default
const { testPlugins } = await import('../support/plugins')
const { LANGUAGE_STORAGE_KEY, TOKEN_STORAGE_KEY, useSessionStore } = await import(
  '../../src/stores/session'
)

const CATALOG = {
  categories: [
    { categoryId: 'category-essen', name: 'Essen', colourHex: '#FFEB3B', sortOrder: 1 },
  ],
  items: [
    {
      id: 'item-bratwurst',
      name: 'Bratwurst',
      categoryId: 'category-essen',
      priceCents: 350,
      sortOrder: 1,
      isAvailable: true,
      stationIds: ['station-kueche'],
      productionMinutes: null,
    },
  ],
  stations: [{ id: 'station-kueche', name: 'Küche', sortOrder: 1 }],
}

const STATION_ORDERS = {
  station: { id: 'station-kueche', name: 'Küche' },
  orders: [],
  asItComes: [],
}

let device: ReturnType<typeof mount> | null = null

function aLaptopThatKnowsThisDeviceAs(deviceKind: 'staffMember' | 'station'): string[] {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      if (url === '/api/session') {
        return new Response(
          JSON.stringify({
            deviceId: 'device-1',
            deviceKind,
            staffMember: deviceKind === 'staffMember' ? { id: 'staff-1', name: 'Anna' } : null,
            station: deviceKind === 'station' ? { id: 'station-kueche', name: 'Küche' } : null,
            language: 'de',
          }),
          { status: 200 },
        )
      }
      if (url === '/api/catalog') {
        return new Response(JSON.stringify(CATALOG), { status: 200 })
      }
      return new Response(JSON.stringify(STATION_ORDERS), { status: 200 })
    }),
  )
  return urls
}

function aLaptopThatCannotBeReached(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => {
      throw new TypeError('Failed to fetch')
    }),
  )
}

function aDeviceThatWasSetUpEarlier() {
  localStorage.setItem(LANGUAGE_STORAGE_KEY, 'de')
  localStorage.setItem(TOKEN_STORAGE_KEY, 'token-the-laptop-issued')
  navigate('/')
  device = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
  return device
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  sessionStorage.setItem('theDoorAnchor', 'yes')
  document.body.innerHTML = ''
})

afterEach(() => {
  device?.unmount()
  device = null
  vi.unstubAllGlobals()
})

describe('a device that starts while the laptop has not said yet whose device it is', () => {
  it('shows the starting screen instead of a screen the device picked by itself', () => {
    aLaptopThatKnowsThisDeviceAs('station')

    const tablet = aDeviceThatWasSetUpEarlier()

    expect(tablet.find('.starting-up').exists()).toBe(true)
  })

  it('shows the spinner alone while the laptop has not answered yet', () => {
    aLaptopThatKnowsThisDeviceAs('station')

    const tablet = aDeviceThatWasSetUpEarlier()

    expect(tablet.find('.starting-up-spinner').exists()).toBe(true)
    expect(tablet.find('.starting-up-message').exists()).toBe(false)
  })

  it('does not put a station tablet in front of the waiter menu while it waits', () => {
    aLaptopThatKnowsThisDeviceAs('station')

    const tablet = aDeviceThatWasSetUpEarlier()

    expect(tablet.find('.catalog').exists()).toBe(false)
  })

  it('carries the app name in the browser tab until the laptop says whose device it is', () => {
    aLaptopThatKnowsThisDeviceAs('station')
    document.title = 'leftover'

    aDeviceThatWasSetUpEarlier()

    expect(document.title).toBe('GastronomyApp')
  })
})

describe('a device once the laptop has said whose device it is', () => {
  it('puts a station tablet on the station page', async () => {
    aLaptopThatKnowsThisDeviceAs('station')

    const tablet = aDeviceThatWasSetUpEarlier()
    await flushPromises()

    expect(tablet.find('.station-page').exists()).toBe(true)
  })

  it('puts a waiter phone in the menu', async () => {
    aLaptopThatKnowsThisDeviceAs('staffMember')

    const phone = aDeviceThatWasSetUpEarlier()
    await flushPromises()

    expect(phone.find('.category-button').text()).toBe('Essen')
  })

  it('never asks for the ordering menu on a station tablet, which works off its own list', async () => {
    const urls = aLaptopThatKnowsThisDeviceAs('station')

    aDeviceThatWasSetUpEarlier()
    await flushPromises()

    expect(urls).not.toContain('/api/catalog')
  })

  it('names the tab after the station the tablet works at', async () => {
    aLaptopThatKnowsThisDeviceAs('station')

    aDeviceThatWasSetUpEarlier()
    await flushPromises()

    expect(document.title).toBe('Küche')
  })

  it('names the ordering tab in the language the phone is set to', async () => {
    aLaptopThatKnowsThisDeviceAs('staffMember')

    aDeviceThatWasSetUpEarlier()
    await flushPromises()

    expect(document.title).toBe('Bestellen')

    useSessionStore().setLanguage('en')
    await flushPromises()

    expect(document.title).toBe('Ordering')
  })
})

describe('a device that cannot reach the laptop while it starts', () => {
  it('says so and names the one thing the volunteer can do', async () => {
    aLaptopThatCannotBeReached()

    const tablet = aDeviceThatWasSetUpEarlier()
    await flushPromises()

    expect(tablet.find('.starting-up-message').text()).toBe(
      'Der Rechner antwortet nicht. Versuchen Sie es noch einmal.',
    )
  })

  it('lands on its own screen once the laptop answers the next try', async () => {
    aLaptopThatCannotBeReached()
    const tablet = aDeviceThatWasSetUpEarlier()
    await flushPromises()

    aLaptopThatKnowsThisDeviceAs('station')
    await tablet.find('.starting-up-try-again').trigger('click')
    await flushPromises()

    expect(tablet.find('.station-page').exists()).toBe(true)
  })
})

describe('a device the laptop cannot answer for while it starts', () => {
  it('says the device could not start and names the one thing the volunteer can do', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('{}', { status: 500 })),
    )

    const tablet = aDeviceThatWasSetUpEarlier()
    await flushPromises()

    expect(tablet.find('.starting-up-message').text()).toBe(
      'Der Rechner hat nicht richtig geantwortet. Versuchen Sie es noch einmal.',
    )
  })
})
