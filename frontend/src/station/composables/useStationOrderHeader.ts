import { computed, toValue, type ComputedRef, type MaybeRefOrGetter } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationOrder } from '../../shared/api/apiTypes'
import { formatFestivalMoment } from '../../shared/core/festivalTimes'
import { deliveryModeColourToken, deliveryModeKey } from '../../shared/core/stationBoard'

export interface StationOrderHeader {
  deliveryText: ComputedRef<string>
  deliveryModeColour: ComputedRef<string>
  orderReference: ComputedRef<string>
  takenByText: ComputedRef<string>
  doneCounter: ComputedRef<string>
}

export function useStationOrderHeader(
  stationOrder: MaybeRefOrGetter<StationOrder>,
): StationOrderHeader {
  const { t, locale } = useI18n()

  const deliveryText = computed(() => t(deliveryModeKey(toValue(stationOrder).deliveryMode)))

  const deliveryModeColour = computed(
    () => `rgb(var(--v-theme-${deliveryModeColourToken(toValue(stationOrder).deliveryMode)}))`,
  )

  const orderReference = computed(() =>
    t('station.order', {
      order: toValue(stationOrder).globalOrderNumber,
      sequence: toValue(stationOrder).stationOrderNumber,
    }),
  )

  const takenByText = computed(() =>
    t('station.takenBy', {
      time: formatFestivalMoment(toValue(stationOrder).createdAtUtc, locale.value),
      name: toValue(stationOrder).staffMemberName,
    }),
  )

  const doneCounter = computed(() =>
    t('station.doneCounter', {
      fulfilled: toValue(stationOrder).fulfilledItemCount,
      total: toValue(stationOrder).itemCount,
    }),
  )

  return { deliveryText, deliveryModeColour, orderReference, takenByText, doneCounter }
}
