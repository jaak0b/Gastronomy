import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from './support/hubConnection'
import { answer, stubLaptopAt, type StubbedLaptop } from './support/laptop'

vi.mock('@microsoft/signalr', async () => (await import('./support/hubConnection')).signalrModuleFake())
vi.mock('../src/shared/router/router', async (importOriginal) => ({
  ...(await importOriginal<typeof import('../src/shared/router/router')>()),
  startOverAt: vi.fn(),
}))

const { navigate, startOverAt } = await import('../src/shared/router/router')
const App = (await import('../src/App.vue')).default
const { testPlugins } = await import('./support/plugins')
const { TOKEN_STORAGE_KEY } = await import('../src/shared/stores/session')

const THE_TABLET = {
  deviceId: 'device-of-the-tablet',
  staffMember: null,
  station: { id: 'station-kueche', name: 'Küche' },
  language: 'de',
}

const THE_PHONE = {
  deviceId: 'device-of-the-phone',
  deviceToken: 'token-of-the-phone',
  staffMember: { id: 'staff-1', name: 'Anna' },
  station: null,
  language: 'de',
}

let device: ReturnType<typeof mount> | null = null

function aLaptopThatTurnsTheTabletIntoAPhone(): StubbedLaptop {
  return stubLaptopAt({
    '/api/enrolment/redeem': answer(THE_PHONE),
    '/api/session': answer(THE_TABLET),
  }).answersEverythingElse(
    answer({
      festival: null,
      categories: [],
      items: [],
      stations: [],
      station: THE_TABLET.station,
      orders: [],
      asItComes: [],
    }),
  )
}

async function aTabletThatScansAWaiterCode() {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.of-the-tablet')
  const laptop = aLaptopThatTurnsTheTabletIntoAPhone()
  navigate('/j/CODE')
  device = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
  await flushPromises()
  await flushPromises()
  return laptop
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

beforeEach(() => {
  sessionStorage.setItem('theDoorAnchor', 'yes')
})

describe('a browser that is already set up and scans a second QR code', () => {
  it('starts the app over, so the live connection is not left on the device it just gave up', async () => {
    await aTabletThatScansAWaiterCode()

    expect(vi.mocked(startOverAt)).toHaveBeenCalledWith('/')
  })

  it('hands the laptop the token it was using, so the laptop can retire that device', async () => {
    const laptop = await aTabletThatScansAWaiterCode()

    expect(laptop.writtenBodies()).toEqual([
      {
        code: 'CODE',
        name: null,
        userAgent: navigator.userAgent,
        previousDeviceToken: 'lookup.of-the-tablet',
      },
    ])
  })

  it('keeps the token it was just given when the laptop retires the device it gave up', async () => {
    await aTabletThatScansAWaiterCode()

    fireHubEvent('DeviceRevoked', { deviceId: 'device-of-the-tablet' })

    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBe('token-of-the-phone')
  })
})
