import { useI18n } from 'vue-i18n'
import type { AdminStationView } from '../../shared/api/generatedSchemas'

export interface StationRefusalReasons {
  switchOffRefusalOf: (station: AdminStationView) => string | null
  removalRefusalOf: (station: AdminStationView) => string | null
}

export function useStationRefusalReasons(): StationRefusalReasons {
  const { t } = useI18n()

  function firstRefusalOf(
    isHoldingItems: boolean,
    holdingItemsText: () => string,
    strandedItemCount: number | null,
    strandedItemsText: (count: number) => string,
  ): string | null {
    if (isHoldingItems) {
      return holdingItemsText()
    }
    if (strandedItemCount !== null && strandedItemCount > 0) {
      return strandedItemsText(strandedItemCount)
    }
    return null
  }

  function switchOffRefusalOf(station: AdminStationView): string | null {
    return firstRefusalOf(
      station.hasItemsToHandOutAtTheRunningFestival,
      () => t('errors.admin.stations.hasUnfinishedItems'),
      station.strandedItemCountWhenSwitchedOff,
      (count) => t('admin.stations.messages.itemsWouldHaveNoStation', { count }, count),
    )
  }

  function removalRefusalOf(station: AdminStationView): string | null {
    return firstRefusalOf(
      station.hasItemsToHandOutAtTheRunningFestival,
      () => t('errors.admin.festivals.stationHasOrdersAtTheFestival'),
      station.strandedItemCountWhenRemovedFromTheFestival,
      (count) => t('errors.admin.stations.itemsWouldHaveNoStation', { count }, count),
    )
  }

  return { switchOffRefusalOf, removalRefusalOf }
}
