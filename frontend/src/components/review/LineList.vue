<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage, DeliveryMode, StationEstimate } from '../../core/apiTypes'
import { assertNever } from '../../core/assertNever'
import { lineCannotBeOrdered, type BasketLineView } from '../../core/basket'
import { collapseLines, type CollapsedLine } from '../../core/collapse'
import { countedName } from '../../core/countedName'
import { queuedMinutesAt, readyInMinutes, sliceEstimateMinutes } from '../../core/estimates'
import { orderSlices } from '../../core/orderSlices'
import { formatPrice, collapsedTotalCents } from '../../core/totals'

interface StationPart {
  stationId: string | null
  stationName: string
  deliveryMode: DeliveryMode
  sliceMinutes: number | null
  entries: CollapsedLine<BasketLineView>[]
}

const props = defineProps<{
  lines: BasketLineView[]
  orderNote: string | null
  language: AppLanguage
  stationNameFor: (stationId: string) => string
  estimates: StationEstimate[]
  deliveryModeFor: (stationId: string) => DeliveryMode
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
  if (stationId === null) {
    return null
  }
  return readyInMinutes(queuedMinutesAt(props.estimates, stationId), line.productionMinutes)
}

const parts = computed<StationPart[]>(() =>
  orderSlices(props.lines).map((slice) => {
    const stationId = slice.stationId
    const deliveryMode = stationId === null ? 'together' : props.deliveryModeFor(stationId)
    const perItem = slice.lines
      .map((line) => minutesOf(line, stationId))
      .filter((minutes): minutes is number => minutes !== null)
    return {
      stationId,
      stationName: stationId === null ? '' : props.stationNameFor(stationId),
      deliveryMode,
      sliceMinutes: sliceEstimateMinutes(perItem, deliveryMode),
      entries: collapseLines(slice.lines, nameOf, (line) => line.note).sort(readingOrder),
    }
  }),
)

function countedNameOf(entry: CollapsedLine<BasketLineView>): string {
  return countedName(entry.quantity, nameOf(entry.line), t)
}

function priceOf(entry: CollapsedLine<BasketLineView>): string {
  return formatPrice(collapsedTotalCents(entry), props.language)
}

function readyTextFor(minutes: number | null): string | null {
  if (minutes === null) {
    return null
  }
  return minutes === 0 ? t('catalog.readyNow') : t('catalog.readyIn', { count: minutes }, minutes)
}

function sliceReadyText(part: StationPart): string | null {
  const minutes = part.sliceMinutes
  if (minutes === null) {
    return null
  }
  return minutes === 0
    ? t('review.sliceReadyNow')
    : t('review.sliceReadyIn', { count: minutes }, minutes)
}

function helpFor(part: StationPart): string {
  switch (part.deliveryMode) {
    case 'together':
      return t('review.deliveryTogetherHelp')
    case 'asItComes':
      return t('review.deliveryAsItComesHelp')
    default:
      return assertNever(part.deliveryMode)
  }
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
        <div class="d-flex align-start">
          <span class="line-name text-body-1 flex-grow-1">{{ countedNameOf(entry) }}</span>
          <span class="price text-body-1">{{ priceOf(entry) }}</span>
        </div>
        <div v-if="entry.line.note !== null" class="line-note text-body-2 text-medium-emphasis ps-4">
          {{ entry.line.note }}
        </div>
        <div
          v-if="readyTextFor(minutesOf(entry.line, part.stationId)) !== null"
          class="line-ready text-body-2 text-medium-emphasis ps-4"
        >
          {{ readyTextFor(minutesOf(entry.line, part.stationId)) }}
        </div>
        <div
          v-if="entry.line.isNoLongerOnTheMenu"
          class="no-longer-on-the-menu text-body-2 text-warning ps-4"
        >
          {{ t('catalog.lineNoLongerOnTheMenu') }}
        </div>
        <div
          v-else-if="entry.line.isSoldOut"
          class="sold-out text-body-2 text-warning ps-4"
        >
          {{ t('catalog.itemSoldOut', { name: entry.line.name }) }}
        </div>
      </div>
      <template v-if="part.stationId !== null">
        <v-divider />
        <div class="delivery-choice px-4 py-3">
          <p class="delivery-question text-body-1 mb-2">
            {{ t('review.deliveryQuestion', { name: part.stationName }) }}
          </p>
          <v-btn-toggle
            class="delivery-modes"
            mandatory
            divided
            :model-value="part.deliveryMode"
            @update:model-value="(mode: DeliveryMode) => choose(part.stationId as string, mode)"
          >
            <v-btn class="delivery-together" value="together" size="large">
              {{ t('review.deliveryTogether') }}
            </v-btn>
            <v-btn class="delivery-as-it-comes" value="asItComes" size="large">
              {{ t('review.deliveryAsItComes') }}
            </v-btn>
          </v-btn-toggle>
          <p class="delivery-help text-body-2 text-medium-emphasis mt-2">{{ helpFor(part) }}</p>
          <p v-if="sliceReadyText(part) !== null" class="slice-ready text-body-1 mt-2">
            {{ sliceReadyText(part) }}
          </p>
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
</style>
