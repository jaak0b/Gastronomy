import { onUnmounted, watch, type Ref } from 'vue'
import type { CatalogItemView } from '../../shared/api/generatedSchemas'
import { itemState } from '../core/catalogItemState'
import { quotedMinutesAt, quoteLinesForStationChoice } from '../core/estimates'
import { candidateStations } from '../core/routingPreview'
import { useEstimatesStore } from '../stores/estimates'
import { useOrderStore } from '../stores/order'

export function useStationChoiceQuotes(
  itemBehindTheStationChoice: Ref<CatalogItemView | null>,
  linesAwaitingStation: Ref<number[]>,
) {
  const estimates = useEstimatesStore()
  const order = useOrderStore()

  watch(itemBehindTheStationChoice, (item) => {
    estimates.stopQuotingStationChoices()
    if (item === null) {
      return
    }
    const unitsAwaitingStation = {
      catalogItemId: item.id,
      units: linesAwaitingStation.value.length > 0 ? linesAwaitingStation.value.length : 1,
    }
    for (const stationId of candidateStations(item)) {
      void estimates.quoteStationChoice(
        stationId,
        quoteLinesForStationChoice(
          order.basketLines,
          linesAwaitingStation.value,
          unitsAwaitingStation,
          stationId,
        ),
      )
    }
  })

  onUnmounted(() => {
    estimates.stopQuotingStationChoices()
  })

  function estimateForTheStationChoice(stationId: string): number | null {
    const item = itemBehindTheStationChoice.value
    if (item === null || itemState(item) === 'soldOut') {
      return null
    }
    return quotedMinutesAt(estimates.stationChoiceQuotes[stationId] ?? [], stationId)
  }

  return { estimateForTheStationChoice }
}
