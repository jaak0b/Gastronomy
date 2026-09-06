import type { ApiErrorBody } from './apiError'
import type { ProductionStatus, StationSlice, StationSliceItem } from './apiTypes'
import { assertNever } from './assertNever'

export type ProductionAdvance = 'inProduction' | 'finished'

export interface SingleItemCard {
  stationOrderId: string
  globalOrderNumber: number
  stationOrderNumber: number
  tableName: string
  note: string | null
  createdAtUtc: string
  item: StationSliceItem
}

export interface StationBoard {
  together: StationSlice[]
  single: SingleItemCard[]
}

export interface SliceAdvance {
  orderItemIds: string[]
  status: ProductionAdvance
}

export type StationFailure =
  | { kind: 'unreachable' }
  | { kind: 'error'; status: number; body: ApiErrorBody | null; raw: unknown }

export function nextProductionStatus(status: ProductionStatus): ProductionAdvance | null {
  switch (status) {
    case 'waiting':
      return 'inProduction'
    case 'inProduction':
      return 'finished'
    case 'finished':
      return null
    default:
      return assertNever(status)
  }
}

function cardsOf(slice: StationSlice): SingleItemCard[] {
  return slice.items
    .filter((item) => item.productionStatus !== 'finished')
    .map((item) => ({
      stationOrderId: slice.stationOrderId,
      globalOrderNumber: slice.globalOrderNumber,
      stationOrderNumber: slice.stationOrderNumber,
      tableName: slice.tableName,
      note: slice.note,
      createdAtUtc: slice.createdAtUtc,
      item,
    }))
}

export function splitSlices(slices: readonly StationSlice[]): StationBoard {
  const together: StationSlice[] = []
  const single: SingleItemCard[] = []
  for (const slice of slices) {
    switch (slice.deliveryMode) {
      case 'together':
        together.push(slice)
        break
      case 'asItComes':
        single.push(...cardsOf(slice))
        break
      default:
        assertNever(slice.deliveryMode)
    }
  }
  return { together, single }
}

function isFullyReady(slice: StationSlice): boolean {
  return slice.items.every((item) => item.productionStatus === 'finished')
}

export function mergeSlices(
  board: readonly StationSlice[],
  answered: readonly StationSlice[],
): StationSlice[] {
  const byId = new Map(board.map((slice) => [slice.stationOrderId, slice]))
  for (const slice of answered) {
    if (isFullyReady(slice)) {
      byId.delete(slice.stationOrderId)
      continue
    }
    byId.set(slice.stationOrderId, slice)
  }
  return [...byId.values()].sort((left, right) => left.stationOrderNumber - right.stationOrderNumber)
}

function idsWith(slice: StationSlice, status: ProductionStatus): string[] {
  return slice.items
    .filter((item) => item.productionStatus === status)
    .map((item) => item.orderItemId)
}

export function sliceAdvance(slice: StationSlice): SliceAdvance | null {
  const waiting = idsWith(slice, 'waiting')
  if (waiting.length > 0) {
    return { orderItemIds: waiting, status: 'inProduction' }
  }
  const inProduction = idsWith(slice, 'inProduction')
  if (inProduction.length > 0) {
    return { orderItemIds: inProduction, status: 'finished' }
  }
  return null
}

export function stationFailureKey(failure: StationFailure): string {
  switch (failure.kind) {
    case 'unreachable':
      return 'station.actionNotReached'
    case 'error':
      return failure.body === null ? 'station.actionFailed' : failure.body.messageKey
    default:
      return assertNever(failure)
  }
}
