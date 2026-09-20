import { computed, toValue, type ComputedRef, type MaybeRefOrGetter } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationOrder } from '../../shared/api/apiTypes'
import { deliveryModeColourToken, deliveryModeKey } from '../../shared/core/stationBoard'

export interface StationOrderHeader {
  deliveryText: ComputedRef<string>
  modeColour: ComputedRef<string>
  orderReference: ComputedRef<string>
  doneCounter: ComputedRef<string>
}

export function useStationOrderHeader(
  stationOrder: MaybeRefOrGetter<StationOrder>,
): StationOrderHeader {
  const { t } = useI18n()

  const deliveryText = computed(() => t(deliveryModeKey(toValue(stationOrder).deliveryMode)))

  const modeColour = computed(
    () => `rgb(var(--v-theme-${deliveryModeColourToken(toValue(stationOrder).deliveryMode)}))`,
  )

  const orderReference = computed(() =>
    t('station.order', {
      order: toValue(stationOrder).globalOrderNumber,
      sequence: toValue(stationOrder).stationOrderNumber,
    }),
  )

  const doneCounter = computed(() =>
    t('station.doneCounter', {
      fulfilled: toValue(stationOrder).fulfilledItemCount,
      total: toValue(stationOrder).itemCount,
    }),
  )

  return { deliveryText, modeColour, orderReference, doneCounter }
}
