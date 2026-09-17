<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  AppLanguage,
  DeliveryMode,
  OrderSettlementRequest,
  StationEstimate,
} from '../../core/apiTypes'
import type { BasketLineView } from '../../core/basket'
import { withEstimate } from '../../core/estimateWording'
import { deliveryModeKey } from '../../core/stationBoard'
import { stationDeliveries, type StationDelivery } from '../../core/stationDeliveries'
import { formatPrice } from '../../core/totals'
import { useKeyboardInset } from '../../composables/useKeyboardInset'
import AmountPaidFields from '../AmountPaidFields.vue'

const props = defineProps<{
  tableName: string
  totalCents: number
  language: AppLanguage
  lines: BasketLineView[]
  estimates: StationEstimate[]
  deliveryModeFor: (stationId: string) => DeliveryMode
}>()

const emit = defineEmits<{
  confirmed: [settlement: OrderSettlementRequest | null]
  cancelled: []
}>()

const { t } = useI18n()
const keyboardInset = useKeyboardInset()

const amount = computed(() => formatPrice(props.totalCents, props.language))

const stations = computed(() =>
  stationDeliveries(props.lines, props.estimates, props.deliveryModeFor).filter(
    (station) => station.stationId !== null,
  ),
)

function deliveryTextOf(station: StationDelivery): string {
  return withEstimate(t(deliveryModeKey(station.deliveryMode)), station.minutes, t, props.language)
}

type SettlementChoice = 'settleLater' | 'settleNow'

const choice = ref<SettlementChoice>('settleLater')
const settlementFields = ref<InstanceType<typeof AmountPaidFields> | null>(null)

const isSettling = computed(() => choice.value === 'settleNow')
const confirmLabel = computed(() =>
  isSettling.value ? t('review.sendAndSettle') : t('review.send'),
)
const canConfirm = computed(
  () =>
    !isSettling.value
    || (settlementFields.value !== null && settlementFields.value.settlement !== null),
)

function confirm(): void {
  if (!isSettling.value) {
    emit('confirmed', null)
    return
  }
  const settlement = settlementFields.value?.settlement
  if (settlement === null || settlement === undefined) {
    return
  }
  emit('confirmed', settlement)
}
</script>

<template>
  <v-dialog class="confirm-send-dialog" model-value persistent fullscreen>
    <v-card class="card d-flex flex-column" :style="{ paddingBottom: `${keyboardInset}px` }">
      <v-card-title class="title">{{ t('review.confirmSendTitle') }}</v-card-title>
      <v-card-text class="body flex-grow-1 d-flex flex-column">
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
        <div class="choices">
          <v-btn-toggle
            class="settlement-choice"
            mandatory
            divided
            border
            :model-value="choice"
            @update:model-value="(chosen: SettlementChoice) => (choice = chosen)"
          >
            <v-btn class="settle-later" value="settleLater" size="large">
              {{ t('review.settleLater') }}
            </v-btn>
            <v-btn class="settle-now" value="settleNow" size="large">
              {{ t('review.settleNow') }}
            </v-btn>
          </v-btn-toggle>
          <AmountPaidFields
            v-if="isSettling"
            ref="settlementFields"
            class="settlement-fields mt-3"
            :total-cents="totalCents"
            :language="language"
          />
        </div>
      </v-card-text>
      <v-card-actions class="actions flex-column align-stretch">
        <v-btn
          class="confirm"
          color="primary"
          variant="flat"
          size="large"
          :disabled="!canConfirm"
          @click="confirm"
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

.choices {
  margin-top: auto;
  padding-top: 1.5rem;
}

.settlement-choice.v-btn-group {
  display: flex;
  width: 100%;
  height: auto;
}

.settlement-choice .v-btn {
  flex: 1 1 0;
  width: auto;
  min-width: 0;
  height: auto;
  min-height: 3.5rem;
  padding-block: 0.75rem;
  font-size: 1.0625rem;
  text-transform: none;
  letter-spacing: normal;
}

.settlement-choice .v-btn :deep(.v-btn__content) {
  display: block;
  flex: 1 1 auto;
  white-space: normal;
  line-height: 1.35;
  text-align: center;
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
