import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppNotices from '../../../src/components/header/AppNotices.vue'
import { useConnectionStore } from '../../../src/stores/connection'
import { useOrderStore } from '../../../src/stores/order'
import { usePrinterStatusStore } from '../../../src/stores/printerStatus'
import type { PrinterStatusRow } from '../../../src/core/apiTypes'
import { testPlugins } from '../../support/plugins'

const KITCHEN_WITHOUT_PAPER: PrinterStatusRow = {
  stationId: 'station-kueche',
  name: 'Küche',
  isOnline: true,
  isPaperEnd: true,
  isPaperNearEnd: false,
  isCoverOpen: false,
  isFaulty: false,
  lastChangedAtUtc: '2026-09-05T18:00:00Z',
}

const KITCHEN_WITH_OPEN_COVER: PrinterStatusRow = {
  ...KITCHEN_WITHOUT_PAPER,
  isPaperEnd: false,
  isCoverOpen: true,
}

const OUTDOOR_BAR_WITHOUT_PAPER: PrinterStatusRow = {
  ...KITCHEN_WITHOUT_PAPER,
  stationId: 'station-bar-aussen',
  name: 'Bar außen',
}

function mountNotices(locale: 'de' | 'en' = 'de') {
  return mount(AppNotices, { global: { plugins: testPlugins(locale) }, attachTo: document.body })
}

function stationBannerKeys(notices: ReturnType<typeof mountNotices>): unknown[] {
  return notices
    .findAllComponents({ name: 'VAlert' })
    .filter((alert) => alert.classes().includes('station-banner'))
    .map((alert) => alert.vm.$.vnode.key)
}

describe('the notices above the screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('states that the laptop cannot be reached', async () => {
    const notices = mountNotices()
    const connection = useConnectionStore()
    connection.state = 'offline'
    await notices.vm.$nextTick()

    expect(notices.get('.connection').text()).toBe(
      'Keine Verbindung zum Laptop. Es wird weiter versucht.',
    )
  })

  it('says nothing at all once the laptop answers', async () => {
    const notices = mountNotices()
    const connection = useConnectionStore()
    connection.state = 'connected'
    await notices.vm.$nextTick()

    expect(notices.find('.connection').exists()).toBe(false)
  })

  it('carries the arrival of an order onto the ordering screen', async () => {
    const notices = mountNotices()
    const order = useOrderStore()
    order.sendState = 'accepted'
    order.acceptedOrderNumber = 1
    await notices.vm.$nextTick()

    expect(notices.get('.sent').text()).toBe('Bestellung 1 ist angekommen.')
  })

  it('adds how many slips are waiting at the station that ran out of paper', async () => {
    const notices = mountNotices()
    const printerStatus = usePrinterStatusStore()
    printerStatus.stations = [KITCHEN_WITHOUT_PAPER]
    printerStatus.waitingCounts = { 'station-kueche': 2 }
    await notices.vm.$nextTick()

    expect(notices.get('.station-banner').text()).toBe(
      'Nehmen Sie weiter Bestellungen auf. Der Drucker an der Ausgabestelle Küche hat kein Papier, und der Bon wird gedruckt, sobald jemand eine Rolle einlegt. Dort warten 2 Bons.',
    )
  })

  it('asks somebody to shut the printer cover that stands open', async () => {
    const notices = mountNotices()
    const printerStatus = usePrinterStatusStore()
    printerStatus.stations = [KITCHEN_WITH_OPEN_COVER]
    await notices.vm.$nextTick()

    expect(notices.get('.station-banner').text()).toBe(
      'Schließen Sie den Deckel des Druckers an der Ausgabestelle Küche. Der Drucker nimmt nichts an, solange der Deckel offen ist, und der Bon wird gedruckt, sobald er wieder geschlossen ist.',
    )
  })

  it('asks for the cover in English too', async () => {
    const notices = mountNotices('en')
    const printerStatus = usePrinterStatusStore()
    printerStatus.stations = [KITCHEN_WITH_OPEN_COVER]
    await notices.vm.$nextTick()

    expect(notices.get('.station-banner').text()).toBe(
      'Close the printer cover at Küche. The printer takes nothing while the cover is open, and the slip prints as soon as it is shut.',
    )
  })

  it('says the same thing in English', async () => {
    const notices = mountNotices('en')
    const printerStatus = usePrinterStatusStore()
    printerStatus.stations = [KITCHEN_WITHOUT_PAPER]
    await notices.vm.$nextTick()

    expect(notices.get('.station-banner').text()).toBe(
      'Keep taking orders. The printer at Küche has no paper, and the slip prints as soon as somebody loads a roll.',
    )
  })

  it('shows both stations that ran out of paper, each one told apart by its station', async () => {
    const notices = mountNotices()
    const printerStatus = usePrinterStatusStore()
    printerStatus.stations = [KITCHEN_WITHOUT_PAPER, OUTDOOR_BAR_WITHOUT_PAPER]
    await notices.vm.$nextTick()

    const shown = notices.findAll('.station-banner')
    expect(shown).toHaveLength(2)
    expect(shown[0].text()).toContain('Küche')
    expect(shown[1].text()).toContain('Bar außen')
    expect(stationBannerKeys(notices)).toEqual(['station-kueche', 'station-bar-aussen'])
  })

  it('lets one of the two stations change without disturbing the other', async () => {
    const notices = mountNotices()
    const printerStatus = usePrinterStatusStore()
    printerStatus.stations = [KITCHEN_WITHOUT_PAPER, OUTDOOR_BAR_WITHOUT_PAPER]
    await notices.vm.$nextTick()

    printerStatus.stations = [
      KITCHEN_WITHOUT_PAPER,
      { ...OUTDOOR_BAR_WITHOUT_PAPER, isPaperEnd: false, isFaulty: true },
    ]
    await notices.vm.$nextTick()

    const shown = notices.findAll('.station-banner')
    expect(shown[0].text()).toBe(
      'Nehmen Sie weiter Bestellungen auf. Der Drucker an der Ausgabestelle Küche hat kein Papier, und der Bon wird gedruckt, sobald jemand eine Rolle einlegt.',
    )
    expect(shown[1].text()).toBe(
      'Sagen Sie jede Bestellung an der Ausgabestelle Bar außen persönlich an. Der Drucker dort nimmt nichts mehr an.',
    )
    expect(stationBannerKeys(notices)).toEqual(['station-kueche', 'station-bar-aussen'])
  })
})
