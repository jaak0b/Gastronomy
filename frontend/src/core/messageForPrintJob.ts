import type { PrintFailureReason, StationOrderSummary } from './apiTypes'
import { assertNever } from './assertNever'

export interface TicketMessage {
  key: string
  parameters: Record<string, string | number>
}

export const OUTER_BOUND_MINUTES = 20

export function formatSequenceNumber(stationOrderNumber: number): string {
  return stationOrderNumber.toString().padStart(3, '0')
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
    return walkOver('printJob.failed', orderNumber, station)
  }
  switch (reason) {
    case 'PaperEnd':
      return atStation('printJob.paperEnd', station)
    case 'CoverOpen':
      return atStation('printJob.coverOpen', station)
    case 'PrinterError':
      return atStation('printJob.printerError', station)
    case 'StationDisabled':
      return walkOver('printJob.stationDisabled', orderNumber, station)
    case 'StationFaulty':
      return walkOver('printJob.stationFaulty', orderNumber, station)
    case 'Unreachable':
    case 'Timeout':
    case 'SocketDropped':
      return walkOver('printJob.failed', orderNumber, station)
    case 'HandledOnPaper':
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
    return walkOver('printJob.failed', orderNumber, station)
  }
  switch (reason) {
    case 'PaperEnd':
    case 'CoverOpen':
      return {
        key: 'printJob.failedAfterWaiting',
        parameters: { number: orderNumber, station, minutes: OUTER_BOUND_MINUTES },
      }
    case 'PrinterError':
      return atStation('printJob.printerError', station)
    case 'StationDisabled':
      return walkOver('printJob.stationDisabled', orderNumber, station)
    case 'StationFaulty':
      return walkOver('printJob.stationFaulty', orderNumber, station)
    case 'Unreachable':
    case 'Timeout':
    case 'SocketDropped':
      return walkOver('printJob.failed', orderNumber, station)
    case 'HandledOnPaper':
      return null
    default:
      return assertNever(reason)
  }
}

export function messageForPrintJob(
  stationOrder: StationOrderSummary,
  orderNumber: number,
): TicketMessage | null {
  const station = stationOrder.stationName
  switch (stationOrder.status) {
    case 'Queued':
    case 'Printing':
    case 'Printed':
      return null
    case 'Blocked':
      return blockedMessage(stationOrder.failureReason, orderNumber, station)
    case 'Failed':
      return failedMessage(stationOrder.failureReason, orderNumber, station)
    case 'Unknown':
      return {
        key: 'printJob.unknown.action',
        parameters: { station, sequence: formatSequenceNumber(stationOrder.stationOrderNumber) },
      }
    case 'HandledOnPaper':
      return { key: 'printJob.handledOnPaper', parameters: {} }
    default:
      return assertNever(stationOrder.status)
  }
}
