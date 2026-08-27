import { describe, expect, it } from 'vitest'
import { messageForTicket } from '../../src/core/messageForTicket'
import type { PrintFailureReason, TicketStatus, TicketSummary } from '../../src/core/apiTypes'

function ticket(
  status: TicketStatus,
  failureReason: PrintFailureReason | null,
  printerHasPaper: boolean | null = true,
): TicketSummary {
  return {
    ticketId: 'ticket-1',
    locationId: 'location-kueche',
    locationName: 'Kueche',
    sequenceNumber: 42,
    status,
    failureReason,
    printerHasPaper,
  }
}

describe('messageForTicket, states that need no message', () => {
  it('says nothing about a ticket that is waiting for its printer', () => {
    const message = messageForTicket(ticket('Queued', null), 137)

    expect(message).toBeNull()
  })

  it('says nothing about a ticket that is printing right now', () => {
    const message = messageForTicket(ticket('Printing', null), 137)

    expect(message).toBeNull()
  })

  it('says nothing about a ticket that printed', () => {
    const message = messageForTicket(ticket('Printed', null), 137)

    expect(message).toBeNull()
  })

  it('says nothing about a ticket a human already resolved', () => {
    const message = messageForTicket(ticket('Failed', 'TicketResolvedByHuman'), 137)

    expect(message).toBeNull()
  })
})

describe('messageForTicket, a station that cannot print yet', () => {
  it('asks for a new roll when the printer has no paper', () => {
    const message = messageForTicket(ticket('Blocked', 'PaperEnd'), 137)

    expect(message).toEqual({ key: 'ticket.paperEnd', parameters: { station: 'Kueche' } })
  })

  it('asks for the cover to be closed when the cover is open', () => {
    const message = messageForTicket(ticket('Blocked', 'CoverOpen'), 137)

    expect(message).toEqual({ key: 'ticket.coverOpen', parameters: { station: 'Kueche' } })
  })

  it('asks for somebody who can look at a printer reporting a fault', () => {
    const message = messageForTicket(ticket('Blocked', 'PrinterError'), 137)

    expect(message).toEqual({ key: 'ticket.printerError', parameters: { station: 'Kueche' } })
  })

  it('sends the server to the station when the station is switched off at the laptop', () => {
    const message = messageForTicket(ticket('Blocked', 'StationDisabled'), 137)

    expect(message).toEqual({
      key: 'ticket.stationDisabled',
      parameters: { number: 137, station: 'Kueche' },
    })
  })
})

describe('messageForTicket, a slip that will not print', () => {
  it('sends the server to the station when the printer never answered', () => {
    const message = messageForTicket(ticket('Failed', 'Unreachable'), 137)

    expect(message).toEqual({ key: 'ticket.failed', parameters: { number: 137, station: 'Kueche' } })
  })

  it('sends the server to the station when the printer timed out', () => {
    const message = messageForTicket(ticket('Failed', 'Timeout'), 137)

    expect(message).toEqual({ key: 'ticket.failed', parameters: { number: 137, station: 'Kueche' } })
  })

  it('sends the server to the station when the connection dropped', () => {
    const message = messageForTicket(ticket('Failed', 'SocketDropped'), 137)

    expect(message).toEqual({ key: 'ticket.failed', parameters: { number: 137, station: 'Kueche' } })
  })

  it('names the wait rather than the cause when the roll never went in', () => {
    const message = messageForTicket(ticket('Failed', 'PaperEnd'), 137)

    expect(message).toEqual({
      key: 'ticket.failedAfterWaiting',
      parameters: { number: 137, station: 'Kueche', minutes: 20 },
    })
  })

  it('names the wait rather than the cause when the cover stayed open', () => {
    const message = messageForTicket(ticket('Failed', 'CoverOpen'), 137)

    expect(message).toEqual({
      key: 'ticket.failedAfterWaiting',
      parameters: { number: 137, station: 'Kueche', minutes: 20 },
    })
  })

  it('says the printer accepts nothing any more when the station was declared faulty', () => {
    const message = messageForTicket(ticket('Failed', 'StationFaulty'), 137)

    expect(message).toEqual({
      key: 'ticket.stationFaulty',
      parameters: { number: 137, station: 'Kueche' },
    })
  })

  it('sends the server to the station when a failure carries no reason at all', () => {
    const message = messageForTicket(ticket('Failed', null), 137)

    expect(message).toEqual({ key: 'ticket.failed', parameters: { number: 137, station: 'Kueche' } })
  })
})

describe('messageForTicket, the question only a human can answer', () => {
  it('sends the server to the pile with the slip number when the outcome is unknown', () => {
    const message = messageForTicket(ticket('Unknown', 'SocketDropped'), 137)

    expect(message).toEqual({
      key: 'ticket.unknown.action',
      parameters: { station: 'Kueche', sequence: '042' },
    })
  })
})

describe('messageForTicket, the two states a human or the laptop produced', () => {
  it('says no slip reached the pile when the station is still on the test printer', () => {
    const message = messageForTicket(ticket('PrintedOnTestPrinter', null), 137)

    expect(message).toEqual({
      key: 'ticket.testPrinter',
      parameters: { number: 137, station: 'Kueche' },
    })
  })

  it('says no slip will be printed once the station took the order from the screen', () => {
    const message = messageForTicket(ticket('HandledOnPaper', null), 137)

    expect(message).toEqual({ key: 'ticket.handledOnPaper', parameters: {} })
  })
})
