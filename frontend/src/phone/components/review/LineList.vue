<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { DeliveryMode, StationQuoteView } from '../../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { lineCannotBeOrdered, type BasketLineView } from '../../core/basket'
import { mergeLinesWithSameArticleAndNote, type CollapsedLine } from '../../../shared/core/collapse'
import { countedName } from '../../core/countedName'
import { withEstimate } from '../../core/estimateWording'
import { deliveryModeKey } from '../../../shared/core/stationBoard'
import { stationDeliveries, type StationDelivery } from '../../core/stationDeliveries'
import { formatPrice, collapsedTotalCents } from '../../core/totals'

interface StationPart extends StationDelivery {
  entries: CollapsedLine<BasketLineView>[]
}

const props = defineProps<{
  lines: BasketLineView[]
  language: AppLanguage
  quotedStations: StationQuoteView[]
  deliveryModeFor: (stationId: string) => DeliveryMode
  changesAreRefused: boolean
}>()
const emit = defineEmits<{
  chooseDeliveryMode: [stationId: string, deliveryMode: DeliveryMode]
}>()

const { t } = useI18n()

function readingOrder(
  left: CollapsedLine<BasketLineView>,
  right: CollapsedLine<BasketLineView>,
): number {
  const byName = left.line.name.localeCompare(right.line.name)
  if (byName !== 0) {
    return byName
  }
  return (left.line.note ?? '').localeCompare(right.line.note ?? '')
}

const parts = computed<StationPart[]>(() =>
  stationDeliveries(props.lines, props.quotedStations, props.deliveryModeFor).map((delivery) => ({
    ...delivery,
    entries: mergeLinesWithSameArticleAndNote(delivery.lines, (line) => line.name, (line) => line.note).sort(readingOrder),
  })),
)

function countedNameFor(entry: CollapsedLine<BasketLineView>): string {
  return countedName(entry.quantity, entry.line.name, t)
}

function deliveryTextFor(deliveryMode: DeliveryMode): string {
  return t(deliveryModeKey(deliveryMode))
}

function priceTextFor(entry: CollapsedLine<BasketLineView>): string {
  const cents = collapsedTotalCents(entry)
  return cents === null ? t('review.unknownPrice') : formatPrice(cents, props.language)
}

function choose(part: StationPart, deliveryMode: DeliveryMode): void {
  if (part.stationId === null) {
    return
  }
  emit('chooseDeliveryMode', part.stationId, deliveryMode)
}
</script>

<template>
  <div class="line-list">
    <v-card
      v-for="part in parts"
      :key="part.stationId ?? part.stationName"
      class="station-part mb-3"
      variant="outlined"
    >
      <v-card-title v-if="part.stationId !== null" class="station-name text-subtitle-1">
        {{ withEstimate(t('review.goesTo', { name: part.stationName }), part.stationMinutes, t, language) }}
      </v-card-title>
      <v-divider v-if="part.stationId !== null" />
      <div
        v-for="entry in part.entries"
        :key="entry.key"
        class="line px-4 py-2"
        :class="{ 'is-unavailable': lineCannotBeOrdered(entry.line) }"
      >
        <fieldset class="line-body">
          <legend v-if="lineCannotBeOrdered(entry.line)" class="reason sold-out text-body-2">
            {{ t('catalog.itemSoldOut', { name: entry.line.name }) }}
          </legend>
          <div class="d-flex align-start">
            <span class="line-name text-body-1 flex-grow-1">
              {{ countedNameFor(entry) }}
            </span>
            <span class="price text-body-1">
              {{ priceTextFor(entry) }}
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
        <div class="delivery-choice px-4 py-2">
          <v-btn-toggle
            class="delivery-modes"
            mandatory
            divided
            border
            :model-value="part.deliveryMode"
            @update:model-value="(mode: DeliveryMode) => choose(part, mode)"
          >
            <v-btn
              class="delivery-together"
              value="together"
              size="large"
              :disabled="changesAreRefused"
            >
              {{ deliveryTextFor('together') }}
            </v-btn>
            <v-btn
              class="delivery-as-it-comes"
              value="asItComes"
              size="large"
              :disabled="changesAreRefused"
            >
              {{ deliveryTextFor('asItComes') }}
            </v-btn>
          </v-btn-toggle>
        </div>
      </template>
    </v-card>
  </div>
</template>

<style scoped>
.line + .line {
  border-top: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}

.station-name {
  padding-block: 0.5rem;
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

.delivery-modes.v-btn-group {
  display: flex;
  width: 100%;
  height: auto;
}

.delivery-modes .v-btn {
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

.delivery-modes .v-btn :deep(.v-btn__content) {
  display: block;
  flex: 1 1 auto;
  white-space: normal;
  line-height: 1.35;
  text-align: center;
}

.delivery-modes .v-btn.delivery-together.v-btn--active {
  background: rgb(var(--v-theme-together));
  color: rgb(var(--v-theme-on-together));
}

.delivery-modes .v-btn.delivery-as-it-comes.v-btn--active {
  background: rgb(var(--v-theme-individual));
  color: rgb(var(--v-theme-on-individual));
}
</style>
