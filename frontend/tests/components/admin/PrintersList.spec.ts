import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import PrintersList from '../../../src/components/admin/printers/PrintersList.vue'
import { testPlugins } from '../../support/plugins'

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
  return mount(PrintersList, { global: { plugins: testPlugins() }, attachTo: document.body })
}

const TEST_PRINTER = {
  printerType: 'TestPrinter',
  printerId: 'printer-kueche',
  name: 'Testdrucker Küche',
  isActive: true,
  isOnline: true,
  isPaperEnd: false,
  isPaperNearEnd: false,
  isCoverOpen: false,
  isFaulty: false,
  waitingTicketCount: 0,
  lastChangedAtUtc: null,
  statusDetail: null,
  stationNames: ['Küche'],
  simulatedFault: 'None',
  simulatedFaultMode: 'Once',
}

const NETWORK_PRINTER = {
  printerType: 'EpsonTmT20ivNetworkPrinter',
  printerId: 'printer-theke',
  name: 'Drucker Theke',
  isActive: true,
  isOnline: true,
  isPaperEnd: false,
  isPaperNearEnd: false,
  isCoverOpen: false,
  isFaulty: false,
  waitingTicketCount: 0,
  lastChangedAtUtc: null,
  statusDetail: null,
  stationNames: [],
  host: '192.168.1.30',
  port: 9100,
}

const ONE_PRINTER = JSON.stringify({ printers: [TEST_PRINTER] })
const ONE_NETWORK_PRINTER = JSON.stringify({ printers: [NETWORK_PRINTER] })

describe('saving a printer', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('names the printer type under the name the laptop binds', async () => {
    const writes = recordWrites(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))
    await page.get('.printer-row .edit').trigger('click')
    await page.get('.printer-form').trigger('submit')

    await vi.waitFor(() => expect(writes.length).toBeGreaterThan(0))
    expect(writes[0].body.printerType).toBe('TestPrinter')
  })

  it('sends the simulated fault of a test printer along with its name', async () => {
    const writes = recordWrites(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))
    await page.get('.printer-row .edit').trigger('click')
    await page.get('.printer-form').trigger('submit')

    await vi.waitFor(() => expect(writes.length).toBeGreaterThan(0))
    expect(Object.keys(writes[0].body).sort()).toEqual(
      ['name', 'printerType', 'simulatedFault', 'simulatedFaultMode'].sort(),
    )
  })

  it('sends the address of a network printer along with its name', async () => {
    const writes = recordWrites(ONE_NETWORK_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))
    await page.get('.printer-row .edit').trigger('click')
    await page.get('.printer-form').trigger('submit')

    await vi.waitFor(() => expect(writes.length).toBeGreaterThan(0))
    expect(Object.keys(writes[0].body).sort()).toEqual(
      ['host', 'name', 'port', 'printerType'].sort(),
    )
    expect(writes[0].body.host).toBe('192.168.1.30')
  })
})

describe('the fault controls of a test printer', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
  })

  it('appear inside the form of a printer that writes to a folder', async () => {
    respondWith(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))
    await page.get('.printer-row .edit').trigger('click')

    expect(page.find('.test-printer-fields').exists()).toBe(true)
  })

  it('stay away from a printer that talks over the network', async () => {
    respondWith(ONE_NETWORK_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))
    await page.get('.printer-row .edit').trigger('click')

    expect(page.find('.test-printer-fields').exists()).toBe(false)
    expect(page.find('.network-printer-fields').exists()).toBe(true)
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
      'Legen Sie für jedes Gerät einen Drucker an. Drucken Sie an jeder Ausgabestelle einen Testbon, bevor die Gäste kommen.',
    )
  })

  it('lists a printer the laptop knows about', async () => {
    respondWith(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))

    expect(page.get('.printer-row .name').text()).toBe('Testdrucker Küche')
  })

  it('names the stations that print on a printer', async () => {
    respondWith(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))

    expect(page.get('.printer-row .stations').text()).toBe(
      'Diese Ausgabestellen drucken auf diesem Drucker: Küche.',
    )
  })

  it('keeps the repair sentence away from a printer that is answering', async () => {
    respondWith(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))

    expect(page.find('.repair-help').exists()).toBe(false)
  })

  it('shows the repair sentence on a printer that is not answering', async () => {
    respondWith(JSON.stringify({ printers: [{ ...TEST_PRINTER, isOnline: false }] }))

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))

    expect(page.get('.repair-help').text()).toBe(
      'Sehen Sie zuerst am Drucker nach Papierstau und Kabel. Danach nimmt der Drucker wieder Bons an.',
    )
  })

  it('says the load failed rather than showing nothing at all', async () => {
    vi.stubGlobal('fetch', vi.fn(async () => Promise.reject(new Error('no network'))))

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.error').exists()).toBe(true))

    expect(page.get('h1').text()).toBe('Drucker')
  })

  it('reads a bare list, because the laptop may answer without an envelope', async () => {
    respondWith(JSON.stringify([TEST_PRINTER]))

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.printer-row').exists()).toBe(true))

    expect(page.get('.printer-row .name').text()).toBe('Testdrucker Küche')
  })
})

describe('adding a printer', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('asks for the printer type in a dialog instead of on the page', async () => {
    respondWith(ONE_PRINTER)

    const page = mountPrinters()
    await vi.waitFor(() => expect(page.find('.new-printer').exists()).toBe(true))

    expect(document.querySelector('.new-printer-dialog')).toBeNull()

    await page.get('.new-printer').trigger('click')

    await vi.waitFor(() => expect(document.querySelector('.new-printer-dialog')).not.toBeNull())
    expect(document.querySelector('.new-printer-dialog .printer-type-field')).not.toBeNull()
  })

})
