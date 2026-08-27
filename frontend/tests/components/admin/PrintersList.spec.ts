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

interface RecordedWrite {
  url: string
  method: string
  body: Record<string, unknown>
}

function recordWrites(body: string) {
  const writes: RecordedWrite[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method !== undefined && init.method !== 'GET') {
        writes.push({
          url,
          method: init.method,
          body: JSON.parse(init.body as string) as Record<string, unknown>,
        })
      }
      return new Response(body, { status: 200 })
    }),
  )
  return writes
}

function mountPrinters() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(PrintersList, { global: { plugins: [i18n] } })
}

const ONE_PRINTER = JSON.stringify({
  printers: [
    {
      stationId: 'station-kueche',
      stationName: 'Küche',
      transportKind: 'Mock',
      host: null,
      port: 9100,
      agentIdentifier: null,
      charactersPerLine: 42,
      codePageName: 'PC858',
      connectTimeoutSeconds: 5,
      jobTimeoutSeconds: 90,
      heartbeatSeconds: 15,
      isEnabled: true,
      isOnline: true,
      isPaperEnd: false,
      isPaperNearEnd: false,
      isCoverOpen: false,
      isFaulty: false,
      waitingTicketCount: 0,
      lastChangedAtUtc: null,
      sharedWithStationNames: [],
      mockFolderPath: null,
    },
  ],
})

describe('saving a printer', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('names the transport under the name the laptop binds', async () => {
    const writes = recordWrites(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))
    await page.get('.printer-row button:nth-of-type(3)').trigger('click')
    await page.get('.printer-form').trigger('submit')

    await vi.waitFor(() => expect(writes.length).toBeGreaterThan(0))
    expect(writes[0].body.transportKind).toBe('Mock')
  })

  it('carries every setting the laptop insists on, so the save is not refused', async () => {
    const writes = recordWrites(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))
    await page.get('.printer-row button:nth-of-type(3)').trigger('click')
    await page.get('.printer-form').trigger('submit')

    await vi.waitFor(() => expect(writes.length).toBeGreaterThan(0))
    expect(Object.keys(writes[0].body).sort()).toEqual(
      [
        'agentIdentifier',
        'charactersPerLine',
        'codePageName',
        'connectTimeoutSeconds',
        'heartbeatSeconds',
        'host',
        'isEnabled',
        'jobTimeoutSeconds',
        'port',
        'transportKind',
      ].sort(),
    )
  })
})

describe('the mock station controls', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('appear for a station that prints to a folder', async () => {
    respondWith(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))

    expect(page.find('.mock-fault-panel').exists()).toBe(true)
  })
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
      'Drucken Sie an jeder Ausgabestelle einen Testbon, bevor die Gäste kommen.',
    )
  })

  it('lists a printer the laptop knows about', async () => {
    respondWith(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))

    expect(page.get('.printer-row .v-card-title').text()).toBe('Küche')
  })

  it('says why the page is empty when no station has been created yet', async () => {
    respondWith(JSON.stringify({ printers: [] }))

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.empty').exists()).toBe(true))

    expect(page.get('.empty').text()).toBe(
      'Legen Sie zuerst eine Ausgabestelle an. Zu jeder Ausgabestelle gehört ein Drucker.',
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

    expect(page.get('.printer-row .v-card-title').text()).toBe('Küche')
  })
})
