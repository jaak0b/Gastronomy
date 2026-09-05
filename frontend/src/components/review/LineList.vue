<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../core/apiTypes'
import type { BasketLineView } from '../../core/basket'
import { collapseLines, type CollapsedLine } from '../../core/collapse'
import { countedName } from '../../core/countedName'
import { formatPrice, collapsedTotalCents } from '../../core/totals'

interface StationSlip {
  stationId: string | null
  stationName: string
  lines: CollapsedLine<BasketLineView>[]
}

const props = defineProps<{
  lines: BasketLineView[]
  orderNote: string | null
  language: AppLanguage
  stationNameFor: (stationId: string) => string
}>()

const { t } = useI18n()

function routedStationId(line: BasketLineView): string | null {
  if (line.stationId !== null) {
    return line.stationId
  }
  return line.candidateStationIds.length === 1 ? line.candidateStationIds[0] : null
}

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

const slips = computed<StationSlip[]>(() => {
  const byStation = new Map<string, BasketLineView[]>()
  props.lines.forEach((line) => {
    const groupKey = routedStationId(line) ?? ''
    byStation.set(groupKey, [...(byStation.get(groupKey) ?? []), line])
  })

  return [...byStation.entries()].map(([groupKey, lines]) => ({
    stationId: groupKey.length === 0 ? null : groupKey,
    stationName: groupKey.length === 0 ? '' : props.stationNameFor(groupKey),
    lines: collapseLines(lines, nameOf, (line) => line.note).sort(readingOrder),
  }))
})

function countedNameOf(entry: CollapsedLine<BasketLineView>): string {
  return countedName(entry.quantity, nameOf(entry.line), t)
}

function priceOf(entry: CollapsedLine<BasketLineView>): string {
  return formatPrice(collapsedTotalCents(entry), props.language)
}
</script>

<template>
  <div class="line-list">
    <v-card
      v-for="slip in slips"
      :key="slip.stationId ?? slip.stationName"
      class="station-slip mb-4"
      variant="outlined"
    >
      <v-card-title v-if="slip.stationId !== null" class="station-name text-subtitle-1">
        {{ t('review.goesTo', { name: slip.stationName }) }}
      </v-card-title>
      <v-divider v-if="slip.stationId !== null" />
      <div
        v-for="(entry, position) in slip.lines"
        :key="position"
        class="line px-4 py-3"
        :class="{ 'is-unavailable': entry.line.isSoldOut || entry.line.isNoLongerOnTheMenu }"
      >
        <div class="d-flex align-start">
          <span class="line-name text-body-1 flex-grow-1">{{ countedNameOf(entry) }}</span>
          <span class="price text-body-1">{{ priceOf(entry) }}</span>
        </div>
        <div v-if="entry.line.note !== null" class="line-note text-body-2 text-medium-emphasis ps-4">
          {{ entry.line.note }}
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
