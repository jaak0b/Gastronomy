import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@microsoft/signalr', async () => (await import('./../support/hubConnection')).signalrModuleFake())

const { navigate } = await import('../../src/router')
const App = (await import('../../src/App.vue')).default
const { testPlugins } = await import('../support/plugins')

const CATALOG = {
  version: '1',
  categories: [{ name: 'Essen', sortOrder: 1 }],
  items: [
    {
      id: 'item-bratwurst',
      name: 'Bratwurst',
      categoryName: 'Essen',
      priceCents: 350,
      sortOrder: 1,
      isAvailable: true,
      stationIds: ['station-kueche'],
    },
  ],
  stations: [{ id: 'station-kueche', name: 'Küche', sortOrder: 1 }],
}

let phone: ReturnType<typeof mount> | null = null

function stubTheLaptop(deviceKind: 'staffMember' | 'station') {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      if (url === '/api/enrolment/redeem') {
        return new Response(
          JSON.stringify({
            deviceId: 'device-1',
            deviceToken: 'token-here',
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
      return new Response(
        JSON.stringify({
          language: 'de',
          deviceKind,
          staffMember: null,
          station: { id: 'station-kueche', name: 'Küche' },
          stations: [],
          slices: [],
          tables: [],
          tableNames: [],
          itemsWithoutAnOrderCount: 0,
        }),
        { status: 200 },
      )
    }),
  )
  return urls
}

async function enrolFromAQrCode() {
  navigate('/j/CODE')
  phone = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
  await flushPromises()
  await flushPromises()
  return phone
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
})

afterEach(() => {
  phone?.unmount()
  phone = null
  vi.unstubAllGlobals()
})

describe('a phone that has just been set up from a QR code', () => {
  it('shows the menu without the waiter having to reload the page', async () => {
    stubTheLaptop('staffMember')

    const app = await enrolFromAQrCode()

    expect(app.text()).toContain('Bratwurst')
  })

  it('asks the laptop for the menu as soon as the phone belongs to a waiter', async () => {
    const urls = stubTheLaptop('staffMember')

    await enrolFromAQrCode()

    expect(urls).toContain('/api/catalog')
  })
})

describe('a station tablet that has just been set up from a QR code', () => {
  it('never asks for the ordering menu, because it only works off the station list', async () => {
    const urls = stubTheLaptop('station')

    await enrolFromAQrCode()

    expect(urls).not.toContain('/api/catalog')
  })
})
