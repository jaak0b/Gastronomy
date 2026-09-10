<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage, DeliveryMode, StationEstimate } from '../../core/apiTypes'
import { assertNever } from '../../core/assertNever'
import type { BasketLineView } from '../../core/basket'
import { withEstimate } from '../../core/estimateWording'
import { stationDeliveries, type StationDelivery } from '../../core/stationDeliveries'
import { formatPrice } from '../../core/totals'

const props = defineProps<{
  settleOnSend: boolean
  tableName: string
  totalCents: number
  language: AppLanguage
  lines: BasketLineView[]
  estimates: StationEstimate[]
  deliveryModeFor: (stationId: string) => DeliveryMode
}>()

const emit = defineEmits<{ confirmed: []; cancelled: [] }>()

const { t } = useI18n()

const amount = computed(() => formatPrice(props.totalCents, props.language))

const stations = computed(() =>
  stationDeliveries(props.lines, props.estimates, props.deliveryModeFor).filter(
    (station) => station.stationId !== null,
  ),
)

function deliveryLabelOf(deliveryMode: DeliveryMode): string {
  switch (deliveryMode) {
    case 'together':
      return t('review.deliveryTogether')
    case 'asItComes':
      return t('review.deliveryAsItComes')
    default:
      return assertNever(deliveryMode)
  }
}

function deliveryTextOf(station: StationDelivery): string {
  return withEstimate(deliveryLabelOf(station.deliveryMode), station.minutes, t)
}

const confirmLabel = computed(() =>
  props.settleOnSend ? t('review.sendAndSettle') : t('review.send'),
)
</script>

<template>
  <v-dialog class="confirm-send-dialog" model-value persistent fullscreen>
    <v-card class="card d-flex flex-column">
      <v-card-title class="title">{{ t('review.confirmSendTitle') }}</v-card-title>
      <v-card-text class="body flex-grow-1">
        <div class="row row-table">
          <span class="label">{{ t('review.confirmSendTable') }}</span>
          <span class="value">{{ tableName }}</span>
        </div>
        <div class="row row-amount">
          <span class="label">{{ t('review.confirmSendAmount') }}</span>
          <span class="value">{{ amount }}</span>
        </div>
        <div v-for="station in stations" :key="station.stationId as string" class="row row-station">
          <span class="label">
            {{ t('review.confirmSendStation', { name: station.stationName }) }}
          </span>
          <span class="value">{{ deliveryTextOf(station) }}</span>
        </div>
      </v-card-text>
      <v-card-actions class="actions flex-column align-stretch">
        <v-btn
          class="confirm"
          color="primary"
          variant="flat"
          size="large"
          @click="emit('confirmed')"
        >
          {{ confirmLabel }}
        </v-btn>
        <v-btn class="cancel" variant="outlined" size="large" @click="emit('cancelled')">
          {{ t('review.confirmSendCancel') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.card {
  height: 100%;
  border-radius: 0;
}

.body {
  font-size: 1.05rem;
  line-height: 1.4;
}

.row {
  display: flex;
  align-items: baseline;
}

.row + .row {
  margin-top: 0.75rem;
}

.label {
  flex: 0 0 auto;
  white-space: nowrap;
  padding-inline-end: 0.75rem;
  color: rgba(var(--v-theme-on-surface), var(--v-medium-emphasis-opacity));
}

.value {
  flex: 1 1 auto;
  min-width: 0;
  overflow-wrap: anywhere;
  font-weight: 600;
}

.title {
  white-space: normal;
  overflow: visible;
  text-overflow: clip;
  line-height: 1.35;
  padding-block: 1rem;
}

.actions {
  padding: 0 1rem 1rem;
}

.actions .v-btn {
  width: 100%;
  min-width: 0;
  height: auto;
  min-height: 3.5rem;
  padding-block: 0.75rem;
  text-transform: none;
  letter-spacing: normal;
}

.actions .v-btn :deep(.v-btn__content) {
  white-space: normal;
}
</style>
