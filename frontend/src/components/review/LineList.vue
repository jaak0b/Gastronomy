<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../core/apiTypes'
import type { BasketLineView } from '../../core/basket'
import { formatPrice, lineTotalCents } from '../../core/totals'

interface StationGroup {
  stationId: string | null
  stationName: string
  lines: { line: BasketLineView; index: number }[]
}

const props = defineProps<{
  lines: BasketLineView[]
  language: AppLanguage
  stationNameFor: (stationId: string) => string
}>()
defineEmits<{
  changeQuantity: [index: number, quantity: number]
  changeStation: [index: number]
  changeNote: [index: number, note: string | null]
}>()

const { t } = useI18n()

function routedStationId(line: BasketLineView): string | null {
  if (line.stationId !== null) {
    return line.stationId
  }
  return line.candidateStationIds.length === 1 ? line.candidateStationIds[0] : null
}

const groups = computed<StationGroup[]>(() => {
  const byStation = new Map<string, StationGroup>()
  props.lines.forEach((line, index) => {
    const stationId = routedStationId(line)
    const groupKey = stationId ?? ''
    const existing = byStation.get(groupKey)
    if (existing === undefined) {
      byStation.set(groupKey, {
        stationId,
        stationName: stationId === null ? '' : props.stationNameFor(stationId),
        lines: [{ line, index }],
      })
    } else {
      existing.lines.push({ line, index })
    }
  })
  return [...byStation.values()]
})

function priceOf(line: BasketLineView): string {
  return formatPrice(lineTotalCents(line), props.language)
}

function nameOf(line: BasketLineView): string {
  return line.name.length > 0 ? line.name : t('catalog.lineNoLongerOnTheMenu')
}

function stationLabelFor(line: BasketLineView): string | null {
  if (line.candidateStationIds.length <= 1) {
    return null
  }
  const stationId = routedStationId(line)
  if (stationId === null) {
    return null
  }
  return t('line.station', { name: props.stationNameFor(stationId) })
}

function noteInput(event: Event): string | null {
  const typed = (event.target as HTMLInputElement).value
  return typed.trim().length === 0 ? null : typed
}
</script>

<template>
  <div class="line-list">
    <section v-for="group in groups" :key="group.stationId ?? group.stationName" class="group">
      <h3 v-if="group.stationId !== null" class="text-subtitle-1 mt-4">
        {{ t('review.goesTo', { name: group.stationName }) }}
      </h3>
      <v-card
        v-for="entry in group.lines"
        :key="entry.index"
        class="line mb-2"
        :class="{ 'is-unavailable': entry.line.isSoldOut || entry.line.isNoLongerOnTheMenu }"
        variant="outlined"
      >
        <v-card-item>
          <v-card-title>
            <span class="quantity">{{ entry.line.quantity }}</span>
            <span class="name ms-2">{{ nameOf(entry.line) }}</span>
            <span class="price ms-2 text-medium-emphasis">{{ priceOf(entry.line) }}</span>
          </v-card-title>
          <v-card-subtitle v-if="stationLabelFor(entry.line) !== null" class="line-station">
            {{ stationLabelFor(entry.line) }}
          </v-card-subtitle>
        </v-card-item>
        <v-card-text>
          <v-text-field
            class="line-note"
            :label="t('catalog.lineNote')"
            :placeholder="t('catalog.lineNotePlaceholder')"
            :model-value="entry.line.note ?? ''"
            @input="$emit('changeNote', entry.index, noteInput($event))"
          />
          <v-alert
            v-if="entry.line.isNoLongerOnTheMenu"
            class="no-longer-on-the-menu"
            type="warning"
            variant="tonal"
            density="compact"
          >
            {{ t('catalog.lineNoLongerOnTheMenu') }}
          </v-alert>
          <v-alert
            v-else-if="entry.line.isSoldOut"
            class="sold-out"
            type="warning"
            variant="tonal"
            density="compact"
          >
            {{ t('catalog.itemSoldOut', { name: entry.line.name }) }}
          </v-alert>
        </v-card-text>
        <v-card-actions>
          <v-btn
            class="less"
            icon="mdi-minus"
            variant="tonal"
            :aria-label="t('catalog.removeOne')"
            @click="$emit('changeQuantity', entry.index, entry.line.quantity - 1)"
          />
          <v-btn
            class="more"
            icon="mdi-plus"
            variant="tonal"
            :aria-label="t('catalog.addOne')"
            @click="$emit('changeQuantity', entry.index, entry.line.quantity + 1)"
          />
          <v-btn
            v-if="entry.line.candidateStationIds.length > 1"
            class="change-station"
            variant="text"
            @click="$emit('changeStation', entry.index)"
          >
            {{ t('line.changeStation') }}
          </v-btn>
        </v-card-actions>
      </v-card>
    </section>
  </div>
</template>
