import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import StationTicketRowComponent from '../../../src/components/station/StationScreenOrderRow.vue'
import type { StationScreenOrderRow, PrintJobStatus } from '../../../src/core/apiTypes'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

interface RowOverrides {
  status?: PrintJobStatus
  canHandleOnPaper?: boolean
  copyNumber?: number
}

function row(overrides: RowOverrides = {}): StationScreenOrderRow {
  return {
    stationOrderId: 'ticket-1',
    orderId: 'order-1',
    globalOrderNumber: 137,
    stationOrderNumber: 42,
    tableName: 'Tisch 12',
    orderCreatedAtUtc: '2026-08-27T19:00:00Z',
    status: overrides.status ?? 'Queued',
    canHandleOnPaper: overrides.canHandleOnPaper ?? false,
    copyNumber: overrides.copyNumber ?? 0,
    orderNote: null,
    items: [{ quantity: 2, itemName: 'Bratwurst', itemNote: 'ohne Zwiebeln' }],
  }
}

function mountRow(stationOrder: StationScreenOrderRow, isPending = false) {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  return mount(StationTicketRowComponent, {
    props: { stationOrder, isPending, takeDelaySeconds: 10, noticeKey: null },
    global: { plugins: [i18n] },
  })
}

const BACKEND_TICKET = {
  stationOrderId: 'ticket-1',
  orderId: 'order-1',
  stationId: 'station-kueche',
  stationName: 'Küche',
  stationOrderNumber: 42,
  globalOrderNumber: 137,
  tableName: 'Tisch 12',
  orderNote: null,
  orderCreatedAtUtc: '2026-08-27T19:00:00Z',
  status: 'Queued',
  copyNumber: 0,
  canHandleOnPaper: false,
  items: [{ quantity: 2, itemName: 'Bratwurst', itemNote: 'ohne Zwiebeln' }],
}

describe('StationScreenOrderRow, fed exactly what the laptop sends', () => {
  it('prints the note the server typed for the kitchen', () => {
    const ticket = mountRow(BACKEND_TICKET as unknown as StationScreenOrderRow)

    expect(ticket.get('.line-note').text()).toBe('Hinweis: ohne Zwiebeln')
  })

  it('says when the slip was ordered', () => {
    const ticket = mountRow(BACKEND_TICKET as unknown as StationScreenOrderRow)

    expect(ticket.get('.row-time').text()).toContain('2026-08-27T19:00:00Z')
  })
})

describe('StationScreenOrderRow, what the row says', () => {
  it('leads with the slip number, the order number and the table', () => {
    const ticket = mountRow(row())

    expect(ticket.get('.headline').text()).toBe('Bon 042, Bestellung 137, Tisch 12')
  })

  it('lists every line with its quantity and its note', () => {
    const ticket = mountRow(row())

    expect(ticket.get('.line').text()).toContain('2 x Bratwurst')
    expect(ticket.get('.line-note').text()).toBe('Hinweis: ohne Zwiebeln')
  })

  it('marks a reprinted slip so the pile and the screen can be told apart', () => {
    const ticket = mountRow(row({ copyNumber: 1 }))

    expect(ticket.get('.reprint-chip').text()).toBe('NACHDRUCK')
  })

  it('leaves the reprint chip off a slip that was never reprinted', () => {
    const ticket = mountRow(row())

    expect(ticket.find('.reprint-chip').exists()).toBe(false)
  })
})

describe('StationScreenOrderRow, the button that is dangerous', () => {
  it('offers the take button when the server says the row may be taken', () => {
    const ticket = mountRow(row({ status: 'Failed', canHandleOnPaper: true }))

    expect(ticket.get('.take').text()).toBe('Übernommen')
  })

  it('puts the table note under the button, where it gets forgotten', () => {
    const ticket = mountRow(row({ status: 'Failed', canHandleOnPaper: true }))

    expect(ticket.get('.take-help').text()).toBe(
      'Schreiben Sie die Tischnummer auf einen Zettel und legen Sie ihn zum Essen.',
    )
  })

  it('offers no button while the station printer is working', () => {
    const ticket = mountRow(row({ canHandleOnPaper: false }))

    expect(ticket.find('.take').exists()).toBe(false)
  })

  it('says to fetch the slip at the printer instead of offering a button', () => {
    const ticket = mountRow(row({ canHandleOnPaper: false }))

    expect(ticket.get('.take-unavailable').text()).toBe(
      'Holen Sie diesen Bon am Drucker. Er ist auf dem Weg zum Drucker und kann nicht übernommen werden.',
    )
  })

  it('says the same on a slip that is being sent to a printer that cannot print', () => {
    const ticket = mountRow(row({ status: 'Sending', canHandleOnPaper: false }))

    expect(ticket.get('.take-unavailable').text()).toBe(
      'Holen Sie diesen Bon am Drucker. Er ist auf dem Weg zum Drucker und kann nicht übernommen werden.',
    )
  })

  it('never offers the button on a slip that is printing right now', () => {
    const ticket = mountRow(row({ status: 'Sending', canHandleOnPaper: false }))

    expect(ticket.find('.take').exists()).toBe(false)
    expect(ticket.get('.status').text()).toBe('Wird gerade gedruckt')
  })
})

describe('StationScreenOrderRow, the ten second delay before sending', () => {
  it('counts down rather than sending as soon as the row is tapped', () => {
    const ticket = mountRow(row({ status: 'Failed', canHandleOnPaper: true }), true)

    expect(ticket.get('.taken-pending').text()).toBe(
      'Tippen Sie auf "Rückgängig", wenn Sie sich vertippt haben. Dieser Bon wird in 10 Sekunden übernommen.',
    )
  })

  it('offers undo in place of the take button while the countdown runs', () => {
    const ticket = mountRow(row({ status: 'Failed', canHandleOnPaper: true }), true)

    expect(ticket.get('.undo').text()).toBe('Rückgängig')
    expect(ticket.find('.take').exists()).toBe(false)
  })

  it('asks for the countdown to be cancelled when the cook taps undo', async () => {
    const ticket = mountRow(row({ status: 'Failed', canHandleOnPaper: true }), true)

    await ticket.get('.undo').trigger('click')

    expect(ticket.emitted('undo')).toHaveLength(1)
  })

  it('starts the delay rather than acknowledging when the cook taps take', async () => {
    const ticket = mountRow(row({ status: 'Failed', canHandleOnPaper: true }))

    await ticket.get('.take').trigger('click')

    expect(ticket.emitted('take')).toHaveLength(1)
  })
})
