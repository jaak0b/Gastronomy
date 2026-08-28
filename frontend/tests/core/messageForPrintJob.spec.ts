import { describe, expect, it } from 'vitest'
import { messageForPrintJob } from '../../src/core/messageForPrintJob'
import type { PrintFailureReason, PrintJobStatus, StationOrderSummary } from '../../src/core/apiTypes'

function ticket(
  status: PrintJobStatus,
  failureReason: PrintFailureReason | null,
  printerHasPaper: boolean | null = true,
): StationOrderSummary {
  return {
    stationOrderId: 'ticket-1',
    stationId: 'station-kueche',
    stationName: 'Kueche',
    stationOrderNumber: 42,
    status,
    failureReason,
    printerHasPaper,
  }
}

describe('messageForPrintJob, states that need no message', () => {
  it('says nothing about a ticket that is waiting for its printer', () => {
    const message = messageForPrintJob(ticket('Queued', null), 137)

    expect(message).toBeNull()
  })

  it('says nothing about a ticket that is printing right now', () => {
    const message = messageForPrintJob(ticket('Printing', null), 137)

    expect(message).toBeNull()
  })

  it('says nothing about a ticket that printed', () => {
    const message = messageForPrintJob(ticket('Printed', null), 137)

    expect(message).toBeNull()
  })

  it('says nothing about a ticket a human already resolved', () => {
    const message = messageForPrintJob(ticket('Failed', 'HandledOnPaper'), 137)

    expect(message).toBeNull()
  })
})

describe('messageForPrintJob, a station that cannot print yet', () => {
  it('asks for a new roll when the printer has no paper', () => {
    const message = messageForPrintJob(ticket('Blocked', 'PaperEnd'), 137)

    expect(message).toEqual({ key: 'printJob.paperEnd', parameters: { station: 'Kueche' } })
  })

  it('asks for the cover to be closed when the cover is open', () => {
    const message = messageForPrintJob(ticket('Blocked', 'CoverOpen'), 137)

    expect(message).toEqual({ key: 'printJob.coverOpen', parameters: { station: 'Kueche' } })
  })

  it('asks for somebody who can look at a printer reporting a fault', () => {
    const message = messageForPrintJob(ticket('Blocked', 'PrinterError'), 137)

    expect(message).toEqual({ key: 'printJob.printerError', parameters: { station: 'Kueche' } })
  })

  it('sends the server to the station when the station is switched off at the laptop', () => {
    const message = messageForPrintJob(ticket('Blocked', 'StationDisabled'), 137)

    expect(message).toEqual({
      key: 'printJob.stationDisabled',
      parameters: { number: 137, station: 'Kueche' },
    })
  })
})

describe('messageForPrintJob, a slip that will not print', () => {
  it('sends the server to the station when the printer never answered', () => {
    const message = messageForPrintJob(ticket('Failed', 'Unreachable'), 137)

    expect(message).toEqual({ key: 'printJob.failed', parameters: { number: 137, station: 'Kueche' } })
  })

  it('sends the server to the station when the printer timed out', () => {
    const message = messageForPrintJob(ticket('Failed', 'Timeout'), 137)

    expect(message).toEqual({ key: 'printJob.failed', parameters: { number: 137, station: 'Kueche' } })
  })

  it('sends the server to the station when the connection dropped', () => {
    const message = messageForPrintJob(ticket('Failed', 'SocketDropped'), 137)

    expect(message).toEqual({ key: 'printJob.failed', parameters: { number: 137, station: 'Kueche' } })
  })

  it('names the wait rather than the cause when the roll never went in', () => {
    const message = messageForPrintJob(ticket('Failed', 'PaperEnd'), 137)

    expect(message).toEqual({
      key: 'printJob.failedAfterWaiting',
      parameters: { number: 137, station: 'Kueche', minutes: 20 },
    })
  })

  it('names the wait rather than the cause when the cover stayed open', () => {
    const message = messageForPrintJob(ticket('Failed', 'CoverOpen'), 137)

    expect(message).toEqual({
      key: 'printJob.failedAfterWaiting',
      parameters: { number: 137, station: 'Kueche', minutes: 20 },
    })
  })

  it('says the printer accepts nothing any more when the station was declared faulty', () => {
    const message = messageForPrintJob(ticket('Failed', 'StationFaulty'), 137)

    expect(message).toEqual({
      key: 'printJob.stationFaulty',
      parameters: { number: 137, station: 'Kueche' },
    })
  })

  it('sends the server to the station when a failure carries no reason at all', () => {
    const message = messageForPrintJob(ticket('Failed', null), 137)

    expect(message).toEqual({ key: 'printJob.failed', parameters: { number: 137, station: 'Kueche' } })
  })
})

describe('messageForPrintJob, the question only a human can answer', () => {
  it('sends the server to the pile with the slip number when the outcome is unknown', () => {
    const message = messageForPrintJob(ticket('Unknown', 'SocketDropped'), 137)

    expect(message).toEqual({
      key: 'printJob.unknown.action',
      parameters: { station: 'Kueche', sequence: '042' },
    })
  })
})

describe('messageForPrintJob, the two states a human or the laptop produced', () => {
  it('says no slip will be printed once the station took the order from the screen', () => {
    const message = messageForPrintJob(ticket('HandledOnPaper', null), 137)

    expect(message).toEqual({ key: 'printJob.handledOnPaper', parameters: {} })
  })
})
