import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import LocationsList from '../../../src/components/admin/locations/LocationsList.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

const STATION_ID = '11111111-1111-1111-1111-111111111111'

const ONE_STATION = JSON.stringify({
  locations: [
    {
      locationId: STATION_ID,
      name: 'Küche',
      sortOrder: 1,
      slipLanguage: 'de',
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
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(LocationsList, { global: { plugins: [i18n] } })
}

async function deactivateFirstStation(list: ReturnType<typeof mountList>) {
  await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
  await list.get('.toggle-active').trigger('click')
  await vi.waitFor(() => expect(list.find('.refusal').exists()).toBe(true))
}

const ONE_STATION_SWITCHED_OFF = JSON.stringify({
  locations: [{ ...JSON.parse(ONE_STATION).locations[0], isActive: false }],
})

describe('a station that is switched off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('says so on its row, so the list is readable at a glance', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(ONE_STATION_SWITCHED_OFF, { status: 200 })),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))

    expect(list.get('.switched-off').text()).toBe('Abgeschaltet')
  })

  it('offers to switch it back on rather than off again', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response(ONE_STATION_SWITCHED_OFF, { status: 200 })),
    )

    const list = mountList()
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))

    expect(list.get('.toggle-active').text()).toBe('Station einschalten')
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
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
    await list.get('.toggle-active').trigger('click')

    await vi.waitFor(() => expect(urls).toContain(`/api/admin/locations/${STATION_ID}/activate`))
  })
})

describe('a station that is switched on', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('offers to switch it off', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(ONE_STATION, { status: 200 })))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))

    expect(list.get('.toggle-active').text()).toBe('Station abschalten')
  })

  it('carries no switched-off marker', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => new Response(ONE_STATION, { status: 200 })))

    const list = mountList()
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))

    expect(list.find('.switched-off').exists()).toBe(false)
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
    await vi.waitFor(() => expect(list.find('li').exists()).toBe(true))
    await list.get('.toggle-active').trigger('click')

    await vi.waitFor(() =>
      expect(urls).toContain(`/api/admin/locations/${STATION_ID}/deactivate`),
    )
  })

})

describe('a station the laptop refuses to switch off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('says items would be left with no station when that is the reason', async () => {
    refuseDeactivationWith('admin.itemsWouldHaveNoStation', { count: 2 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Ordnen Sie 2 Artikeln zuerst eine andere Station zu oder nehmen Sie sie von der Karte. Sonst bleiben sie ohne Station und können nicht bestellt werden.',
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
      'Ordnen Sie 1 Artikel zuerst einer anderen Station zu oder nehmen Sie ihn von der Karte. Sonst bleibt er ohne Station und kann nicht bestellt werden.',
    )
  })

  it('says open slips when open slips really are the reason', async () => {
    refuseDeactivationWith('admin.locations.openTickets', { count: 3 })

    const list = mountList()
    await deactivateFirstStation(list)

    expect(list.get('.refusal').text()).toBe(
      'Diese Station hat noch 3 offene Bons und kann jetzt nicht abgeschaltet werden.',
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
