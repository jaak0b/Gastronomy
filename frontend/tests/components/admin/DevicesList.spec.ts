import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import DevicesList from '../../../src/components/admin/devices/DevicesList.vue'
import { useConnectionStore } from '../../../src/stores/connection'
import { pressInDialog, testPlugins } from '../../support/plugins'

const PHONE_ID = '11111111-1111-1111-1111-111111111111'
const TABLET_ID = '22222222-2222-2222-2222-222222222222'

const TWO_DEVICES = JSON.stringify({
  devices: [
    {
      deviceId: PHONE_ID,
      deviceKind: 'staffMember',
      ownerId: '99999999-9999-9999-9999-999999999999',
      ownerName: 'Anna',
      language: 'de',
      createdAtUtc: '2026-09-05T17:00:00Z',
      lastSeenAtUtc: '2026-09-05T19:00:00Z',
    },
    {
      deviceId: TABLET_ID,
      deviceKind: 'station',
      ownerId: '88888888-8888-8888-8888-888888888888',
      ownerName: 'Küche',
      language: 'de',
      createdAtUtc: '2026-09-05T17:00:00Z',
      lastSeenAtUtc: '2026-09-05T19:00:00Z',
    },
  ],
})

function stubTheLaptop() {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      return new Response(TWO_DEVICES, { status: 200 })
    }),
  )
  return urls
}

function mountList() {
  return mount(DevicesList, { global: { plugins: testPlugins() }, attachTo: document.body })
}

describe('the list of devices in the admin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    stubTheLaptop()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says which waiter a phone belongs to', async () => {
    const list = mountList()
    await vi.waitFor(() => expect(list.find('.device-row').exists()).toBe(true))

    expect(list.findAll('.device-row .owner')[0].text()).toBe('Telefon von Anna')
  })

  it('says which station a tablet belongs to', async () => {
    const list = mountList()
    await vi.waitFor(() => expect(list.find('.device-row').exists()).toBe(true))

    expect(list.findAll('.device-row .owner')[1].text()).toBe(
      'Tablet an der Ausgabestelle Küche',
    )
  })
})

describe('the device screen the admin has left', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('no longer reloads the list when the laptop reports a change', async () => {
    const urls = stubTheLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find('.device-row').exists()).toBe(true))

    list.unmount()
    urls.length = 0
    await useConnectionStore().refetchAll()

    expect(urls).toEqual([])
  })
})

describe('signing a device out', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks before it happens', async () => {
    const urls = stubTheLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find('.device-row').exists()).toBe(true))

    await list.get('.revoke').trigger('click')

    expect(urls.some((url) => url.endsWith('/revoke'))).toBe(false)
  })

  it('signs a tablet out at its own address, the same way a phone is signed out', async () => {
    const urls = stubTheLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find('.device-row').exists()).toBe(true))

    await list.findAll('.revoke')[1].trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() => expect(urls).toContain(`/api/admin/devices/${TABLET_ID}/revoke`))
  })
})
