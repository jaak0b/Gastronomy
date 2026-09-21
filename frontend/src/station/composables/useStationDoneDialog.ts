import { computed, ref, type ComputedRef, type Ref } from 'vue'
import { StationOrderQueueView } from '../../shared/api/generatedSchemas'
import { selectedUnits, type ItemLine } from '../../shared/core/stationBoard'
import { useStationStore } from '../stores/station'

export interface StationDoneDialogState {
  doneStationOrder: Ref<StationOrderQueueView | null>
  doneUnits: ComputedRef<ItemLine[]>
  openDone: (stationOrder: StationOrderQueueView, orderItemIds: string[]) => void
  closeDone: () => void
  confirmDone: () => Promise<void>
}

export function useStationDoneDialog(): StationDoneDialogState {
  const station = useStationStore()

  const doneStationOrder = ref<StationOrderQueueView | null>(null)
  const doneItemIds = ref<string[]>([])

  const doneUnits = computed<ItemLine[]>(() =>
    doneStationOrder.value === null
      ? []
      : selectedUnits([doneStationOrder.value], doneItemIds.value),
  )

  function openDone(stationOrder: StationOrderQueueView, orderItemIds: string[]): void {
    doneStationOrder.value = stationOrder
    doneItemIds.value = orderItemIds
  }

  function closeDone(): void {
    doneStationOrder.value = null
    doneItemIds.value = []
  }

  async function confirmDone(): Promise<void> {
    const orderItemIds = doneItemIds.value
    closeDone()
    await station.fulfill(orderItemIds)
  }

  return { doneStationOrder, doneUnits, openDone, closeDone, confirmDone }
}
