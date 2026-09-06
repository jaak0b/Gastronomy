import type { DeliveryMode, ProductionStatus } from './apiTypes'
import { assertNever } from './assertNever'
import type { ProductionAdvance } from './stationBoard'

export function productionStatusKey(status: ProductionStatus): string {
  switch (status) {
    case 'waiting':
      return 'station.status.waiting'
    case 'inProduction':
      return 'station.status.inProduction'
    case 'finished':
      return 'station.status.finished'
    default:
      return assertNever(status)
  }
}

export function deliveryModeKey(deliveryMode: DeliveryMode): string {
  switch (deliveryMode) {
    case 'together':
      return 'station.deliveryTogether'
    case 'asItComes':
      return 'station.deliveryAsItComes'
    default:
      return assertNever(deliveryMode)
  }
}

export function advanceItemKey(advance: ProductionAdvance): string {
  switch (advance) {
    case 'inProduction':
      return 'station.start'
    case 'finished':
      return 'station.finish'
    default:
      return assertNever(advance)
  }
}

export function advanceSliceKey(advance: ProductionAdvance): string {
  switch (advance) {
    case 'inProduction':
      return 'station.startAll'
    case 'finished':
      return 'station.finishAll'
    default:
      return assertNever(advance)
  }
}

export function openItemProductionKey(status: ProductionStatus): string {
  switch (status) {
    case 'waiting':
      return 'openItems.production.waiting'
    case 'inProduction':
      return 'openItems.production.inProduction'
    case 'finished':
      return 'openItems.production.finished'
    default:
      return assertNever(status)
  }
}

export function openItemDeliveryKey(deliveryMode: DeliveryMode): string {
  switch (deliveryMode) {
    case 'together':
      return 'openItems.delivery.together'
    case 'asItComes':
      return 'openItems.delivery.asItComes'
    default:
      return assertNever(deliveryMode)
  }
}
