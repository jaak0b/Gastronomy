import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../support/hubConnection'

vi.mock('@microsoft/signalr', async () => (await import('../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../src/stores/connection')
const { TOKEN_STORAGE_KEY, useSessionStore } = await import('../../src/stores/session')

const THE_TABLET = {
  deviceId: 'device-of-the-tablet',
  deviceKind: 'station',
  staffMember: null,
  station: { id: 'station-kueche', name: 'Küche' },
  language: 'de',
}

function aLaptopThatKnowsTheTablet(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async () => new Response(JSON.stringify(THE_TABLET), { status: 200 })),
  )
}

async function aTabletListeningForTheLaptop() {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'token-of-the-tablet')
  aLaptopThatKnowsTheTablet()
  const session = useSessionStore()
  session.watchForBeingSignedOut()
  await session.loadSession()
  await useConnectionStore().connect({ deviceToken: 'token-of-the-tablet' })
  return session
}

describe('a device the laptop says is no longer set up', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('signs the browser out when it is the device the browser is holding', async () => {
    const session = await aTabletListeningForTheLaptop()

    fireHubEvent('DeviceRevoked', { deviceId: 'device-of-the-tablet' })

    expect(session.deviceToken).toBeNull()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('leaves the browser at work when it is some other device', async () => {
    const session = await aTabletListeningForTheLaptop()

    fireHubEvent('DeviceRevoked', { deviceId: 'device-of-somebody-elses-phone' })

    expect(session.deviceToken).toBe('token-of-the-tablet')
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBe('token-of-the-tablet')
  })

  it('leaves the newer setup of the same browser in storage when it signs itself out', async () => {
    const session = await aTabletListeningForTheLaptop()
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-of-the-phone')

    fireHubEvent('DeviceRevoked', { deviceId: 'device-of-the-tablet' })

    expect(session.deviceToken).toBeNull()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBe('token-of-the-phone')
  })

  it('asks the laptop what became of it when it has not heard which device it is yet', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-of-the-tablet')
    aLaptopThatKnowsTheTablet()
    const session = useSessionStore()
    session.watchForBeingSignedOut()
    await useConnectionStore().connect({ deviceToken: 'token-of-the-tablet' })

    fireHubEvent('DeviceRevoked', { deviceId: 'device-of-the-tablet' })

    await vi.waitFor(() => expect(session.deviceId).toBe('device-of-the-tablet'))
    expect(session.deviceToken).toBe('token-of-the-tablet')
  })
})
