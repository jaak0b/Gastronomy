import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../support/hubConnection'

vi.mock('@microsoft/signalr', async () => (await import('./../support/hubConnection')).signalrModuleFake())
vi.mock('../../src/router', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../../src/router')>()),
  startOverAt: vi.fn(),
}))

const { navigate, startOverAt } = await import('../../src/router')
const App = (await import('../../src/App.vue')).default
const { testPlugins } = await import('../support/plugins')
const { TOKEN_STORAGE_KEY } = await import('../../src/stores/session')

const THE_TABLET = {
  deviceId: 'device-of-the-tablet',
  deviceKind: 'station',
  staffMember: null,
  station: { id: 'station-kueche', name: 'Küche' },
  language: 'de',
}

const THE_PHONE = {
  deviceId: 'device-of-the-phone',
  deviceToken: 'token-of-the-phone',
  deviceKind: 'staffMember',
  staffMember: { id: 'staff-1', name: 'Anna' },
  station: null,
  language: 'de',
}

let device: ReturnType<typeof mount> | null = null

function aLaptopThatTurnsTheTabletIntoAPhone(): unknown[] {
  const bodies: unknown[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, options?: RequestInit) => {
      if (url === '/api/enrolment/redeem') {
        bodies.push(JSON.parse(String(options?.body ?? 'null')))
        return new Response(JSON.stringify(THE_PHONE), { status: 200 })
      }
      if (url === '/api/session') {
        return new Response(JSON.stringify(THE_TABLET), { status: 200 })
      }
      return new Response(
        JSON.stringify({
          categories: [],
          items: [],
          stations: [],
          station: THE_TABLET.station,
          slices: [],
        }),
        { status: 200 },
      )
    }),
  )
  return bodies
}

async function aTabletThatScansAWaiterCode() {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'token-of-the-tablet')
  const bodies = aLaptopThatTurnsTheTabletIntoAPhone()
  navigate('/j/CODE')
  device = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
  await flushPromises()
  await flushPromises()
  return bodies
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  forgetHubEvents()
  vi.mocked(startOverAt).mockClear()
})

afterEach(() => {
  device?.unmount()
  device = null
  vi.unstubAllGlobals()
})

describe('a browser that is already set up and scans a second QR code', () => {
  it('starts the app over, so the live connection is not left on the device it just gave up', async () => {
    await aTabletThatScansAWaiterCode()

    expect(vi.mocked(startOverAt)).toHaveBeenCalledWith('/')
  })

  it('hands the laptop the token it was using, so the laptop can retire that device', async () => {
    const bodies = await aTabletThatScansAWaiterCode()

    expect(bodies).toEqual([
      {
        code: 'CODE',
        name: null,
        userAgent: navigator.userAgent,
        previousDeviceToken: 'token-of-the-tablet',
      },
    ])
  })

  it('keeps the token it was just given when the laptop retires the device it gave up', async () => {
    await aTabletThatScansAWaiterCode()

    fireHubEvent('DeviceRevoked', { deviceId: 'device-of-the-tablet' })

    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBe('token-of-the-phone')
  })
})
