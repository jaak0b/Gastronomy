import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { hubEventsRegistered } from '../support/hubConnection'

vi.mock('@microsoft/signalr', async () => (await import('../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../src/stores/connection')
const { useAdminDevicesStore } = await import('../../src/stores/admin/devices')
const { useAdminEnrolmentStore } = await import('../../src/stores/admin/enrolment')
const { useAdminStaffStore } = await import('../../src/stores/admin/staff')
const { useAdminStationsStore } = await import('../../src/stores/admin/stations')

const EMPTY_LISTS = { staffMembers: [], stations: [], devices: [] }

function stubTheLaptop(): string[] {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      return new Response(JSON.stringify(EMPTY_LISTS), { status: 200 })
    }),
  )
  return urls
}

describe('the waiter list of the admin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    hubEventsRegistered.length = 0
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is reloaded while the screen that asked for it is open', async () => {
    const urls = stubTheLaptop()
    useAdminStaffStore().listen()

    await useConnectionStore().refetchAll()

    expect(urls).toEqual(['/api/admin/staff-members'])
  })

  it('is left alone once the admin has moved to another screen', async () => {
    const urls = stubTheLaptop()
    const stopListening = useAdminStaffStore().listen()

    stopListening()
    await useConnectionStore().refetchAll()

    expect(urls).toEqual([])
  })
})

describe('the station list of the admin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    hubEventsRegistered.length = 0
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is reloaded while the screen that asked for it is open', async () => {
    const urls = stubTheLaptop()
    useAdminStationsStore().listen()

    await useConnectionStore().refetchAll()

    expect(urls).toEqual(['/api/admin/stations'])
  })

  it('is left alone once the admin has moved to another screen', async () => {
    const urls = stubTheLaptop()
    const stopListening = useAdminStationsStore().listen()

    stopListening()
    await useConnectionStore().refetchAll()

    expect(urls).toEqual([])
  })
})

describe('the device list of the admin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    hubEventsRegistered.length = 0
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is reloaded while the screen that asked for it is open', async () => {
    const urls = stubTheLaptop()
    useAdminDevicesStore().listen()

    await useConnectionStore().refetchAll()

    expect(urls).toEqual(['/api/admin/devices'])
  })

  it('is left alone once the admin has moved to another screen', async () => {
    const urls = stubTheLaptop()
    const stopListening = useAdminDevicesStore().listen()

    stopListening()
    await useConnectionStore().refetchAll()

    expect(urls).toEqual([])
  })
})

describe('the handler that answers a finished enrolment', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    hubEventsRegistered.length = 0
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sits on the hub while the screen that asked for it is open', async () => {
    stubTheLaptop()
    useAdminEnrolmentStore().listen(() => undefined)

    await useConnectionStore().connect({})

    expect(hubEventsRegistered).toEqual(['EnrolmentCompleted'])
  })

  it('is taken off the hub once the admin has moved to another screen', async () => {
    stubTheLaptop()
    const stopListening = useAdminEnrolmentStore().listen(() => undefined)

    stopListening()
    await useConnectionStore().connect({})

    expect(hubEventsRegistered).toEqual([])
  })
})
