import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import AdminOverview from '../../../src/components/admin/overview/AdminOverview.vue'
import { useAdminFestivalsStore } from '../../../src/stores/admin/festivals'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

const SUMMER = {
  festivalId: 'fest-1',
  name: 'Sommerfest',
  startsAtUtc: '2026-07-18T10:00:00Z',
  endsAtUtc: '2026-07-19T02:00:00Z',
  isHidden: false,
  isRunning: true,
  stationCount: 1,
  menuItemCount: 1,
  orderCount: 0,
}

function answerFor(url: string, payload: Record<string, unknown>): Response {
  const body = url.startsWith('/api/admin/festivals')
    ? { festivals: [SUMMER] }
    : { stations: [], items: [], categories: [], ...payload }
  return new Response(JSON.stringify(body), { status: 200 })
}

function theFestivalIsOpen(): void {
  useAdminFestivalsStore().pick('fest-1')
}

function mountOverview(locale: 'de' | 'en' = 'de') {
  const i18n = createI18n({ legacy: false, locale, messages: { de, en } })
  return mount(AdminOverview, { global: { plugins: [i18n] } })
}

describe('the address the phones connect to', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string) => answerFor(url, {})),
    )
  })

  it('stands on the overview, because the program window no longer shows it', () => {
    const overview = mountOverview()

    expect(overview.find('.phone-address').exists()).toBe(true)
  })

  it('names the address a phone types into its browser', () => {
    const overview = mountOverview()

    expect(overview.get('.phone-address').text()).toBe(
      'Die Telefone erreichen den Laptop unter http://localhost:3000.',
    )
  })

  it('names it in English too', () => {
    const overview = mountOverview('en')

    expect(overview.get('.phone-address').text()).toBe('Phones reach the laptop at http://localhost:3000.')
  })

  it('shows no QR code, because the only enrolment path is the one on the waiters page', () => {
    const overview = mountOverview()

    expect(overview.find('img').exists()).toBe(false)
    expect(overview.find('canvas').exists()).toBe(false)
  })
})

describe('what the overview says is still missing', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  function stubStations(stations: unknown[]) {
    vi.stubGlobal('fetch', vi.fn(async (url: string) => answerFor(url, { stations })))
    theFestivalIsOpen()
  }

  it('asks for the tablet of a station that has none yet', async () => {
    stubStations([
      {
        stationId: 'station-kueche',
        name: 'Küche',
        sortOrder: 1,
        isActive: true,
        hasDevice: false,
        isAtTheFestival: true,
      },
    ])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('.readiness-row').map((row) => row.text())).toContain(
      'Richten Sie das Tablet für Küche ein. Ohne Tablet sieht diese Ausgabestelle ihre Bestellungen nicht.',
    )
  })

  it('says nothing about a station whose tablet is already set up', async () => {
    stubStations([
      {
        stationId: 'station-kueche',
        name: 'Küche',
        sortOrder: 1,
        isActive: true,
        hasDevice: true,
        isAtTheFestival: true,
      },
    ])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('.readiness-row').map((row) => row.text())).not.toContain(
      'Richten Sie das Tablet für Küche ein. Ohne Tablet sieht diese Ausgabestelle ihre Bestellungen nicht.',
    )
  })
})

describe('the category the admin needs before any item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  function stubCategories(categories: unknown[]) {
    vi.stubGlobal('fetch', vi.fn(async (url: string) => answerFor(url, { categories })))
    theFestivalIsOpen()
  }

  it('asks for a category while the laptop holds none', async () => {
    stubCategories([])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('.readiness-row').map((row) => row.text())).toContain(
      'Legen Sie mindestens eine Kategorie an, zum Beispiel Speisen und Getränke.',
    )
  })

  it('says nothing about categories once one exists', async () => {
    stubCategories([
      { categoryId: 'category-drinks', name: 'Getränke', colourHex: '#C62828', sortOrder: 1, isActive: true },
    ])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('.readiness-row').map((row) => row.text())).not.toContain(
      'Legen Sie mindestens eine Kategorie an, zum Beispiel Speisen und Getränke.',
    )
  })
})

describe('the overview before a festival is open', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('asks for a festival while the laptop knows none', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async (url: string) =>
          new Response(
            JSON.stringify(
              url.startsWith('/api/admin/festivals')
                ? { festivals: [] }
                : { stations: [], items: [], categories: [] },
            ),
            { status: 200 },
          ),
      ),
    )

    const overview = mountOverview()
    await flushPromises()

    expect(overview.get('.missing-festival').text()).toBe('Legen Sie zuerst ein Fest an.')
    expect(overview.findAll('.readiness-row')).toHaveLength(0)
  })

  it('asks the admin to open one while none is open', async () => {
    vi.stubGlobal('fetch', vi.fn(async (url: string) => answerFor(url, {})))

    const overview = mountOverview()
    await flushPromises()

    expect(overview.get('.no-festival-picked').text()).toBe(
      'Öffnen Sie unter "Feste" das Fest, um das es heute geht.',
    )
    expect(overview.findAll('.readiness-row')).toHaveLength(0)
  })

  it('says that nothing is running when the festival the admin opened has not started', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async (url: string) =>
          new Response(
            JSON.stringify(
              url.startsWith('/api/admin/festivals')
                ? { festivals: [{ ...SUMMER, isRunning: false }] }
                : { stations: [], items: [], categories: [] },
            ),
            { status: 200 },
          ),
      ),
    )
    theFestivalIsOpen()

    const overview = mountOverview()
    await flushPromises()

    expect(overview.get('.not-running').text()).toBe(
      'Zurzeit läuft kein Fest. Sehen Sie unter "Feste" nach.',
    )
  })
})
