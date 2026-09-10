<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage, DeliveryMode, StationEstimate } from '../../core/apiTypes'
import { lineCannotBeOrdered, type BasketLineView } from '../../core/basket'
import { collapseLines, type CollapsedLine } from '../../core/collapse'
import { countedName } from '../../core/countedName'
import { lineEstimateMinutes } from '../../core/estimates'
import { withEstimate } from '../../core/estimateWording'
import { orderSlices } from '../../core/orderSlices'
import { stationDeliveries, type StationDelivery } from '../../core/stationDeliveries'
import { formatPrice, collapsedTotalCents } from '../../core/totals'

interface StationPart extends StationDelivery {
  entries: CollapsedLine<BasketLineView>[]
}

const props = defineProps<{
  lines: BasketLineView[]
  orderNote: string | null
  language: AppLanguage
  estimates: StationEstimate[]
  deliveryModeFor: (stationId: string) => DeliveryMode
  changesAreRefused: boolean
}>()
const emit = defineEmits<{
  chooseDeliveryMode: [stationId: string, deliveryMode: DeliveryMode]
}>()

const { t } = useI18n()

function nameOf(line: BasketLineView): string {
  return line.name.length > 0 ? line.name : t('catalog.lineNoLongerOnTheMenu')
}

function readingOrder(
  left: CollapsedLine<BasketLineView>,
  right: CollapsedLine<BasketLineView>,
): number {
  const byName = nameOf(left.line).localeCompare(nameOf(right.line))
  if (byName !== 0) {
    return byName
  }
  return (left.line.note ?? '').localeCompare(right.line.note ?? '')
}

function minutesOf(line: BasketLineView, stationId: string | null): number | null {
  return lineEstimateMinutes(props.estimates, stationId, line.productionMinutes)
}

const parts = computed<StationPart[]>(() => {
  const entriesPerSlice = orderSlices(props.lines).map((slice) =>
    collapseLines(slice.lines, nameOf, (line) => line.note).sort(readingOrder),
  )
  return stationDeliveries(props.lines, props.estimates, props.deliveryModeFor).map(
    (delivery, position) => ({ ...delivery, entries: entriesPerSlice[position] }),
  )
})

function countedNameOf(entry: CollapsedLine<BasketLineView>, stationId: string | null): string {
  const counted = countedName(entry.quantity, nameOf(entry.line), t)
  if (lineCannotBeOrdered(entry.line)) {
    return counted
  }
  return withEstimate(counted, minutesOf(entry.line, stationId), t)
}

function togetherLabelOf(part: StationPart): string {
  return withEstimate(t('review.deliveryTogether'), part.minutes, t)
}

function priceOf(entry: CollapsedLine<BasketLineView>): string | null {
  const cents = collapsedTotalCents(entry)
  return cents === null ? null : formatPrice(cents, props.language)
}

function choose(stationId: string, deliveryMode: DeliveryMode): void {
  emit('chooseDeliveryMode', stationId, deliveryMode)
}
</script>

<template>
  <div class="line-list">
    <v-card
      v-for="part in parts"
      :key="part.stationId ?? part.stationName"
      class="station-part mb-4"
      variant="outlined"
    >
      <v-card-title v-if="part.stationId !== null" class="station-name text-subtitle-1">
        {{ t('review.goesTo', { name: part.stationName }) }}
      </v-card-title>
      <v-divider v-if="part.stationId !== null" />
      <div
        v-for="(entry, position) in part.entries"
        :key="position"
        class="line px-4 py-3"
        :class="{ 'is-unavailable': lineCannotBeOrdered(entry.line) }"
      >
        <fieldset class="line-body">
          <legend
            v-if="entry.line.isNoLongerOnTheMenu"
            class="reason no-longer-on-the-menu text-body-2"
          >
            {{ t('catalog.lineNoLongerOnTheMenu') }}
          </legend>
          <legend v-else-if="entry.line.isSoldOut" class="reason sold-out text-body-2">
            {{ t('catalog.itemSoldOut', { name: entry.line.name }) }}
          </legend>
          <legend
            v-else-if="entry.line.isNoLongerPreparedAtItsStation"
            class="reason station-no-longer-prepares-it text-body-2"
          >
            {{ t('catalog.lineStationNoLongerPreparesIt') }}
          </legend>
          <div class="d-flex align-start">
            <span class="line-name text-body-1 flex-grow-1">
              {{ countedNameOf(entry, part.stationId) }}
            </span>
            <span v-if="priceOf(entry) !== null" class="price text-body-1">
              {{ priceOf(entry) }}
            </span>
          </div>
          <div
            v-if="entry.line.note !== null"
            class="line-note text-body-2 text-medium-emphasis ps-4"
          >
            {{ entry.line.note }}
          </div>
        </fieldset>
      </div>
      <template v-if="part.stationId !== null">
        <v-divider />
        <div class="delivery-choice px-4 py-3">
          <v-btn-toggle
            class="delivery-modes"
            mandatory
            divided
            border
            direction="vertical"
            :model-value="part.deliveryMode"
            @update:model-value="(mode: DeliveryMode) => choose(part.stationId as string, mode)"
          >
            <v-btn
              class="delivery-together"
              value="together"
              size="large"
              :disabled="changesAreRefused"
            >
              {{ togetherLabelOf(part) }}
            </v-btn>
            <v-btn
              class="delivery-as-it-comes"
              value="asItComes"
              size="large"
              :disabled="changesAreRefused"
            >
              {{ t('review.deliveryAsItComes') }}
            </v-btn>
          </v-btn-toggle>
        </div>
      </template>
      <template v-if="orderNote !== null">
        <v-divider />
        <div class="order-note px-4 py-3">
          <span class="label text-body-2 text-medium-emphasis">{{ t('catalog.orderNote') }}</span>
          <div class="text-body-1">{{ orderNote }}</div>
        </div>
      </template>
    </v-card>
  </div>
</template>

<style scoped>
.line + .line {
  border-top: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.line-body {
  border: 0;
  margin: 0;
  padding: 0;
  min-inline-size: 0;
}

.is-unavailable .line-body {
  border: 2px solid rgb(var(--v-theme-warning));
  border-radius: 8px;
  padding: 0.5rem 0.75rem 0.75rem;
}

.reason {
  color: rgb(var(--v-theme-warning));
  padding-inline: 0.5rem;
  margin-inline-start: 0.25rem;
  line-height: 1.3;
}

.line-name {
  min-width: 0;
  overflow-wrap: anywhere;
}

.price {
  flex: 0 0 auto;
  white-space: nowrap;
  padding-inline-start: 0.75rem;
}

.delivery-modes {
  display: flex;
  width: 100%;
}

.delivery-modes .v-btn {
  width: 100%;
  height: auto;
  min-height: 3.5rem;
  padding-block: 0.75rem;
  font-size: 1.0625rem;
  text-transform: none;
  letter-spacing: normal;
}

.delivery-modes .v-btn :deep(.v-btn__content) {
  white-space: normal;
  line-height: 1.35;
}
</style>
