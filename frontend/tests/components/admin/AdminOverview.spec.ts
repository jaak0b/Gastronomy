import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import AdminOverview from '../../../src/components/admin/overview/AdminOverview.vue'
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

function stubLaptop(festivals: unknown[], rest: Record<string, unknown> = {}): string[] {
  const urls: string[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      urls.push(url)
      const body = url.startsWith('/api/admin/festivals')
        ? { festivals }
        : { stations: [], items: [], categories: [], ...rest }
      return new Response(JSON.stringify(body), { status: 200 })
    }),
  )
  return urls
}

function mountOverview(locale: 'de' | 'en' = 'de') {
  const i18n = createI18n({ legacy: false, locale, messages: { de, en } })
  return mount(AdminOverview, { global: { plugins: [i18n] } })
}

describe('the overview while no festival exists at all', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('asks for a festival and lists nothing else', async () => {
    stubLaptop([])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.get('.missing-festival').text()).toBe('Legen Sie zuerst ein Fest an.')
    expect(overview.findAll('.readiness-row')).toHaveLength(0)
  })
})

describe('the overview while no festival is running', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('says so and lists nothing else', async () => {
    stubLaptop([{ ...SUMMER, isRunning: false }])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.get('.not-running').text()).toBe(
      'Zurzeit ist kein Fest aktiv. Sehen Sie unter "Feste" nach.',
    )
    expect(overview.findAll('.readiness-row')).toHaveLength(0)
  })

  it('says it in English too', async () => {
    stubLaptop([{ ...SUMMER, isRunning: false }])

    const overview = mountOverview('en')
    await flushPromises()

    expect(overview.get('.not-running').text()).toBe('No festival is active right now. Look under "Festivals".')
  })

  it('asks the laptop for nothing that belongs to a festival', async () => {
    const urls = stubLaptop([{ ...SUMMER, isRunning: false }])

    mountOverview()
    await flushPromises()

    expect(urls.some((url) => url.includes('festivalId='))).toBe(false)
  })
})

describe('the overview of the festival that is running', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    window.history.replaceState({}, '', '/admin')
  })

  it('names that festival', async () => {
    stubLaptop([SUMMER])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.get('.running-festival').text()).toBe('Sommerfest')
  })

  it('reads the stations and the items of that festival', async () => {
    const urls = stubLaptop([SUMMER])

    mountOverview()
    await flushPromises()

    expect(urls).toContain('/api/admin/stations?festivalId=fest-1')
    expect(urls).toContain('/api/admin/items?festivalId=fest-1')
  })

  it('asks for a station while that festival has none', async () => {
    stubLaptop([SUMMER])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('.readiness-row').map((row) => row.text())).toContain(
      'Fügen Sie diesem Fest mindestens eine Ausgabestelle hinzu, zum Beispiel Küche und Theke.',
    )
  })

  it('asks for the tablet of a station that has none yet', async () => {
    stubLaptop([SUMMER], {
      stations: [
        {
          stationId: 'station-kueche',
          name: 'Küche',
          sortOrder: 1,
          isActive: true,
          hasDevice: false,
          isAtTheFestival: true,
        },
      ],
    })

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('.readiness-row').map((row) => row.text())).toContain(
      'Richten Sie das Tablet für Küche ein. Ohne Tablet sieht diese Ausgabestelle ihre Bestellungen nicht.',
    )
  })

  it('says nothing about a station whose tablet is already set up', async () => {
    stubLaptop([SUMMER], {
      stations: [
        {
          stationId: 'station-kueche',
          name: 'Küche',
          sortOrder: 1,
          isActive: true,
          hasDevice: true,
          isAtTheFestival: true,
        },
      ],
    })

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('.readiness-row').map((row) => row.text())).not.toContain(
      'Richten Sie das Tablet für Küche ein. Ohne Tablet sieht diese Ausgabestelle ihre Bestellungen nicht.',
    )
  })

  it('asks for a category while the laptop holds none', async () => {
    stubLaptop([SUMMER])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('.readiness-row').map((row) => row.text())).toContain(
      'Legen Sie mindestens eine Kategorie an, zum Beispiel Speisen und Getränke.',
    )
  })

  it('says nothing about categories once one exists', async () => {
    stubLaptop([SUMMER], {
      categories: [
        {
          categoryId: 'category-drinks',
          name: 'Getränke',
          colourHex: '#C62828',
          sortOrder: 1,
          isActive: true,
        },
      ],
    })

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('.readiness-row').map((row) => row.text())).not.toContain(
      'Legen Sie mindestens eine Kategorie an, zum Beispiel Speisen und Getränke.',
    )
  })

  it('shows no QR code, because the only enrolment path is the one on the waiters page', async () => {
    stubLaptop([SUMMER])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.find('img').exists()).toBe(false)
    expect(overview.find('canvas').exists()).toBe(false)
  })
})
