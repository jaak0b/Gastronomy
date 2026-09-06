import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import AdminOverview from '../../../src/components/admin/overview/AdminOverview.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

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
      vi.fn(
        async () =>
          new Response(JSON.stringify({ stations: [], items: [] }), { status: 200 }),
      ),
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
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () => new Response(JSON.stringify({ stations, items: [] }), { status: 200 }),
      ),
    )
  }

  it('asks for the tablet of a station that has none yet', async () => {
    stubStations([
      { stationId: 'station-kueche', name: 'Küche', sortOrder: 1, isActive: true, hasDevice: false },
    ])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('.readiness-row').map((row) => row.text())).toContain(
      'Richten Sie das Tablet für Küche ein. Ohne Tablet sieht diese Ausgabestelle ihre Bestellungen nicht.',
    )
  })

  it('says nothing about a station whose tablet is already set up', async () => {
    stubStations([
      { stationId: 'station-kueche', name: 'Küche', sortOrder: 1, isActive: true, hasDevice: true },
    ])

    const overview = mountOverview()
    await flushPromises()

    expect(overview.findAll('.readiness-row').map((row) => row.text())).not.toContain(
      'Richten Sie das Tablet für Küche ein. Ohne Tablet sieht diese Ausgabestelle ihre Bestellungen nicht.',
    )
  })
})
