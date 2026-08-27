import { describe, expect, it } from 'vitest'
import { presentOrderState } from '../../src/core/orderStateMachine'
import type { OrderStatus, OrderSummary, TicketStatus } from '../../src/core/apiTypes'

function ticket(status: TicketStatus, sequenceNumber: number) {
  return {
    ticketId: `ticket-${sequenceNumber}`,
    locationId: 'location-kueche',
    locationName: 'Kueche',
    sequenceNumber,
    status,
    failureReason: null,
    printerHasPaper: true,
  }
}

function order(status: OrderStatus, ticketStatuses: TicketStatus[]): OrderSummary {
  return {
    orderId: 'order-1',
    globalOrderNumber: 137,
    tableLabel: 'Tisch 12',
    totalCents: 1050,
    status,
    createdAtUtc: '2026-08-27T19:00:00Z',
    tickets: ticketStatuses.map((ticketStatus, index) => ticket(ticketStatus, index + 1)),
  }
}

describe('presentOrderState', () => {
  it('shows an accepted order as printing, because the server sees no third state', () => {
    const state = presentOrderState(order('Accepted', ['Queued']))

    expect(state).toBe('Printing')
  })

  it('shows an order a printer is working on as printing', () => {
    const state = presentOrderState(order('Printing', ['Printing']))

    expect(state).toBe('Printing')
  })

  it('shows a fully printed order as printed', () => {
    const state = presentOrderState(order('Printed', ['Printed', 'Printed']))

    expect(state).toBe('Printed')
  })

  it('shows a printed order the station took off the screen as taken by the station', () => {
    const state = presentOrderState(order('Printed', ['Printed', 'HandledOnPaper']))

    expect(state).toBe('HandledOnPaper')
  })

  it('shows an order printed only on the test printer as printed', () => {
    const state = presentOrderState(order('Printed', ['PrintedOnTestPrinter']))

    expect(state).toBe('Printed')
  })

  it('shows an order that needs checking as needing attention', () => {
    const state = presentOrderState(order('NeedsAttention', ['Failed']))

    expect(state).toBe('NeedsAttention')
  })

  it('keeps an order that needs checking on attention even when one ticket was taken on paper', () => {
    const state = presentOrderState(order('NeedsAttention', ['HandledOnPaper', 'Unknown']))

    expect(state).toBe('NeedsAttention')
  })
})
