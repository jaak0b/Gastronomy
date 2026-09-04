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

function mountNotices(locale: 'de' | 'en' = 'de') {
  return mount(AppNotices, { global: { plugins: testPlugins(locale) }, attachTo: document.body })
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

  it('says the same thing in English', async () => {
    const notices = mountNotices('en')
    const printerStatus = usePrinterStatusStore()
    printerStatus.stations = [KITCHEN_WITHOUT_PAPER]
    await notices.vm.$nextTick()

    expect(notices.get('.station-banner').text()).toBe(
      'Keep taking orders. The printer at Küche has no paper, and the slip prints as soon as somebody loads a roll.',
    )
  })
})
