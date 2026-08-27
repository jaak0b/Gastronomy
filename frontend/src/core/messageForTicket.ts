import type { PrintFailureReason, TicketSummary } from './apiTypes'
import { assertNever } from './assertNever'

export interface TicketMessage {
  key: string
  parameters: Record<string, string | number>
}

export const OUTER_BOUND_MINUTES = 20

export function formatSequenceNumber(sequenceNumber: number): string {
  return sequenceNumber.toString().padStart(3, '0')
}

function walkOver(key: string, orderNumber: number, station: string): TicketMessage {
  return { key, parameters: { number: orderNumber, station } }
}

function atStation(key: string, station: string): TicketMessage {
  return { key, parameters: { station } }
}

function blockedMessage(
  reason: PrintFailureReason | null,
  orderNumber: number,
  station: string,
): TicketMessage | null {
  if (reason === null) {
    return walkOver('ticket.failed', orderNumber, station)
  }
  switch (reason) {
    case 'PaperEnd':
      return atStation('ticket.paperEnd', station)
    case 'CoverOpen':
      return atStation('ticket.coverOpen', station)
    case 'PrinterError':
      return atStation('ticket.printerError', station)
    case 'StationDisabled':
      return walkOver('ticket.stationDisabled', orderNumber, station)
    case 'StationFaulty':
      return walkOver('ticket.stationFaulty', orderNumber, station)
    case 'Unreachable':
    case 'Timeout':
    case 'SocketDropped':
      return walkOver('ticket.failed', orderNumber, station)
    case 'TicketResolvedByHuman':
      return null
    default:
      return assertNever(reason)
  }
}

function failedMessage(
  reason: PrintFailureReason | null,
  orderNumber: number,
  station: string,
): TicketMessage | null {
  if (reason === null) {
    return walkOver('ticket.failed', orderNumber, station)
  }
  switch (reason) {
    case 'PaperEnd':
    case 'CoverOpen':
      return {
        key: 'ticket.failedAfterWaiting',
        parameters: { number: orderNumber, station, minutes: OUTER_BOUND_MINUTES },
      }
    case 'PrinterError':
      return atStation('ticket.printerError', station)
    case 'StationDisabled':
      return walkOver('ticket.stationDisabled', orderNumber, station)
    case 'StationFaulty':
      return walkOver('ticket.stationFaulty', orderNumber, station)
    case 'Unreachable':
    case 'Timeout':
    case 'SocketDropped':
      return walkOver('ticket.failed', orderNumber, station)
    case 'TicketResolvedByHuman':
      return null
    default:
      return assertNever(reason)
  }
}

export function messageForTicket(
  ticket: TicketSummary,
  orderNumber: number,
): TicketMessage | null {
  const station = ticket.locationName
  switch (ticket.status) {
    case 'Queued':
    case 'Printing':
    case 'Printed':
      return null
    case 'Blocked':
      return blockedMessage(ticket.failureReason, orderNumber, station)
    case 'Failed':
      return failedMessage(ticket.failureReason, orderNumber, station)
    case 'Unknown':
      return {
        key: 'ticket.unknown.action',
        parameters: { station, sequence: formatSequenceNumber(ticket.sequenceNumber) },
      }
    case 'PrintedOnTestPrinter':
      return walkOver('ticket.testPrinter', orderNumber, station)
    case 'HandledOnPaper':
      return { key: 'ticket.handledOnPaper', parameters: {} }
    default:
      return assertNever(ticket.status)
  }
}
