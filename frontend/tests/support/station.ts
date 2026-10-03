import type { StationOrderQueueView, StationQueueItemView } from '../../src/shared/api/generatedSchemas'
import { useSessionStore } from '../../src/shared/stores/session'
import { answer, type LaptopReply } from './laptop'

export const KITCHEN = { id: 'station-kueche', name: 'Küche' }

export function item(
  orderItemId: string,
  itemName: string,
  fulfilledAtUtc: string | null = null,
): StationQueueItemView {
  return { orderItemId, itemName, note: null, fulfilledAtUtc }
}

export function stationOrder(overrides: Partial<StationOrderQueueView> = {}): StationOrderQueueView {
  return {
    stationOrderId: 'station-order-1',
    globalOrderNumber: 137,
    stationOrderNumber: 12,
    tableName: 'Tisch 3',
    staffMemberName: 'Anna',
    deliveryMode: 'together',
    createdAtUtc: '2026-09-05T18:00:00Z',
    isHiddenFromAsItComesQueue: false,
    itemCount: 2,
    fulfilledItemCount: 0,
    items: [item('a', 'Bratwurst'), item('b', 'Pommes')],
    ...overrides,
  }
}

export function aQueue(
  orders: StationOrderQueueView[],
  asItComes: StationOrderQueueView[] = [],
): LaptopReply {
  return answer({ station: KITCHEN, orders, asItComes })
}

export function enrolledStationTablet(): void {
  const session = useSessionStore()
  session.deviceToken = 'token-here'
  session.station = KITCHEN
}
