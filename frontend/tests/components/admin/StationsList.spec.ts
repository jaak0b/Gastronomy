import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import StationsList from '../../../src/components/admin/stations/StationsList.vue'
import { pressInDialog, testPlugins, waitForDialog } from '../../support/plugins'

const STATION_ID = '11111111-1111-1111-1111-111111111111'

const ONE_STATION = JSON.stringify({
  stations: [
    {
      stationId: STATION_ID,
      name: 'Küche',
      sortOrder: 1,
      isActive: true,
      accessKey: 'key-kueche',
      breakGlassUrl: 'http://192.168.1.20:5000/s/key-kueche',
      transportKind: 'Mock',
      host: null,
      port: 9100,
      isEnabled: true,
      isOnline: true,
      isPaperEnd: false,
      isCoverOpen: false,
      isFaulty: false,
    },
  ],
})

function refuseDeactivationWith(messageKey: string, parameters: Record<string, unknown>) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
        return new Response(
          JSON.stringify({ code: 'Conflict', messageKey, parameters, details: null }),
          { status: 409 },
        )
      }
      return new Response(ONE_STATION, { status: 200 })
    }),
  )
}

function mountList() {
  return mount(StationsList, { global: { plugins: testPlugins() }, attachTo: document.body })
}


async function deactivateFirstStation(list: ReturnType<typeof mountList>) {
  await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
  await list.get('.deactivate').trigger('click')
  await pressInDialog('.confirm')
  await vi.waitFor(() => expect(list.find('.refusal').exists()).toBe(true))
}

const ONE_STATION_SWITCHED_OFF = JSON.stringify({
  stations: [{ ...JSON.parse(ONE_STATION).stations[0], isActive: false }],
})

describe('switching a station off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('asks before it happens, rather than acting on the first tap', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(ONE_STATION, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(urls.some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('says that the orders already placed are kept', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(ONE_STATION, { status: 200 })))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')

    await waitForDialog()

    expect(document.querySelector('.confirm-body')!.textContent).toContain('bleiben gespeichert')
  })

  it('does nothing when the question is answered with no', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(ONE_STATION, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.cancel')

    expect(urls.some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('switches the station off once the question is answered with yes', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(ONE_STATION, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/stations/${STATION_ID}/deactivate`),
    )
  })
})

describe('a station that is switched off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('is left out of the list until the admin asks to see deactivated entries', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(ONE_STATION_SWITCHED_OFF, { status: 200 })),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.empty').exists()).toBe(false))

    expect(list.find('.station-row').exists()).toBe(false)
  })

  it('says so on its row once it is shown', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(ONE_STATION_SWITCHED_OFF, { status: 200 })),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.empty').exists()).toBe(false))
    await list.get('.show-deactivated input').setValue(true)
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))

    expect(list.get('.deactivated').text()).toBe('Deaktiviert')
  })

  it('offers to switch it back on without asking a question first', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(ONE_STATION_SWITCHED_OFF, { status: 200 })),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.empty').exists()).toBe(false))
    await list.get('.show-deactivated input').setValue(true)
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))

    expect(list.get('.reactivate').text()).toBe('Ausgabestelle einschalten')
  })

  it('switches it back on at its own address', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(ONE_STATION_SWITCHED_OFF, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.empty').exists()).toBe(false))
    await list.get('.show-deactivated input').setValue(true)
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.reactivate').trigger('click')

    await vi.waitFor(() => expect(urls).toContain(`/api/admin/stations/${STATION_ID}/activate`))
  })
})

describe('the buttons beside a station', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('switch that station off at its own address', async () => {
    const urls: string[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => {
        urls.push(url)
        return new Response(ONE_STATION, { status: 200 })
      }),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('.station-row').exists()).toBe(true))
    await list.get('.deactivate').trigger('click')
    await pressInDialog('.confirm')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/stations/${STATION_ID}/deactivate`),
    )
  })

})

describe('a station the laptop refuses to switch off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('says items would be left with no station when that is the reason', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 2 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Ordnen Sie 2 Artikeln zuerst eine andere Ausgabestelle zu oder nehmen Sie sie von der Karte. Sonst bleiben sie ohne Ausgabestelle und können nicht bestellt werden.',
    )
  })

  it('never blames open slips for an orphaned-items refusal', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 2 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).not.toContain('offene Bons')
  })

  it('takes the singular form when a single item would be left behind', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 1 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Ordnen Sie 1 Artikel zuerst einer anderen Ausgabestelle zu oder nehmen Sie ihn von der Karte. Sonst bleibt er ohne Ausgabestelle und kann nicht bestellt werden.',
    )
  })

  it('says open slips when open slips really are the reason', async () => {
    refuseDeactivationWith('admin.stations.openTickets', { count: 3 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Diese Ausgabestelle hat noch 3 offene Bons und kann jetzt nicht abgeschaltet werden.',
    )
  })

  it('falls back to a general message for a reason this app does not know', async () => {
    refuseDeactivationWith('admin.somethingAddedLater', { count: 3 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Das hat nicht geklappt. Versuchen Sie es noch einmal, und laden Sie die Seite neu, wenn es wieder nicht klappt.',
    )
  })
})

