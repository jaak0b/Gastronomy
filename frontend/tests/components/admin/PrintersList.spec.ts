import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import PrintersList from '../../../src/components/admin/printers/PrintersList.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

function respondWith(body: string, status = 200) {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(body, { status })))
}

function mountPrinters() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(PrintersList, { global: { plugins: [i18n] } })
}

const ONE_PRINTER = JSON.stringify({
  printers: [
    {
      locationId: 'location-kueche',
      locationName: 'Küche',
      transport: 'Mock',
      host: null,
      port: null,
      isEnabled: true,
      isOnline: true,
      isPaperEnd: false,
      isPaperNearEnd: false,
      isCoverOpen: false,
      isFaulty: false,
      waitingTicketCount: 0,
      lastChangedAtUtc: null,
      sharedWithLocationNames: [],
      mockFolderPath: null,
    },
  ],
})

describe('the printers page', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('shows its heading before any data has arrived', () => {
    respondWith(ONE_PRINTER)

    const page = mountPrinters()

    expect(page.get('h1').text()).toBe('Drucker')
  })

  it('explains what the page is for', () => {
    respondWith(ONE_PRINTER)

    const page = mountPrinters()

    expect(page.get('.help').text()).toBe(
      'Drucken Sie an jeder Station einen Testbon, bevor die Gäste kommen.',
    )
  })

  it('lists a printer the laptop knows about', async () => {
    respondWith(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))

    expect(page.get('.printer-row h2').text()).toBe('Küche')
  })

  it('says why the page is empty when no station has been created yet', async () => {
    respondWith(JSON.stringify({ printers: [] }))

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.empty').exists()).toBe(true))

    expect(page.get('.empty').text()).toBe(
      'Legen Sie zuerst eine Station an. Zu jeder Station gehört ein Drucker.',
    )
  })

  it('says the load failed rather than showing nothing at all', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Promise.reject(new Error('no network'))))

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.error').exists()).toBe(true))

    expect(page.get('.error').text()).toBe(
      'Laden Sie die Seite neu. Die Daten konnten nicht geladen werden.',
    )
  })

  it('keeps its heading when the laptop answers with a shape it did not expect', async () => {
    respondWith(JSON.stringify({ somethingElse: true }))

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.error').exists()).toBe(true))

    expect(page.get('h1').text()).toBe('Drucker')
  })

  it('reads a bare list, because the laptop may answer without an envelope', async () => {
    respondWith(ONE_PRINTER.slice(ONE_PRINTER.indexOf('['), -1))

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))

    expect(page.get('.printer-row h2').text()).toBe('Küche')
  })
})
