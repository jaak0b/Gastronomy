<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage } from '../../core/apiTypes'
import type { BasketLineView } from '../../core/basket'
import { formatPrice, lineTotalCents } from '../../core/totals'

interface StationGroup {
  locationId: string | null
  locationName: string
  lines: { line: BasketLineView; index: number }[]
}

const props = defineProps<{
  lines: BasketLineView[]
  language: AppLanguage
  locationNameFor: (locationId: string) => string
}>()
defineEmits<{
  changeQuantity: [index: number, quantity: number]
  changeStation: [index: number]
  changeNote: [index: number, note: string | null]
}>()

const { t } = useI18n()

function routedLocationId(line: BasketLineView): string | null {
  if (line.productionLocationId !== null) {
    return line.productionLocationId
  }
  return line.candidateLocationIds.length === 1 ? line.candidateLocationIds[0] : null
}

const groups = computed<StationGroup[]>(() => {
  const byLocation = new Map<string, StationGroup>()
  props.lines.forEach((line, index) => {
    const locationId = routedLocationId(line)
    const groupKey = locationId ?? ''
    const existing = byLocation.get(groupKey)
    if (existing === undefined) {
      byLocation.set(groupKey, {
        locationId,
        locationName: locationId === null ? '' : props.locationNameFor(locationId),
        lines: [{ line, index }],
      })
    } else {
      existing.lines.push({ line, index })
    }
  })
  return [...byLocation.values()]
})

function priceOf(line: BasketLineView): string {
  return formatPrice(lineTotalCents(line), props.language)
}

function nameOf(line: BasketLineView): string {
  return line.name.length > 0 ? line.name : t('catalog.lineNoLongerOnTheMenu')
}

function stationLabelFor(line: BasketLineView): string | null {
  if (line.candidateLocationIds.length <= 1) {
    return null
  }
  const locationId = routedLocationId(line)
  if (locationId === null) {
    return null
  }
  return t('line.station', { name: props.locationNameFor(locationId) })
}

function noteInput(event: Event): string | null {
  const typed = (event.target as HTMLInputElement).value
  return typed.trim().length === 0 ? null : typed
}
</script>

<template>
  <div class="line-list">
    <section v-for="group in groups" :key="group.locationId ?? group.locationName" class="group">
      <h3 v-if="group.locationId !== null">
        {{ t('review.goesTo', { name: group.locationName }) }}
      </h3>
      <div
        v-for="entry in group.lines"
        :key="entry.index"
        class="line"
        :class="{ 'is-unavailable': entry.line.isSoldOut || entry.line.isNoLongerOnTheMenu }"
      >
        <span class="quantity">{{ entry.line.quantity }}</span>
        <span class="name">{{ nameOf(entry.line) }}</span>
        <span class="price">{{ priceOf(entry.line) }}</span>
        <button
          type="button"
          class="less"
          :aria-label="t('catalog.removeOne')"
          @click="$emit('changeQuantity', entry.index, entry.line.quantity - 1)"
        >
          {{ t('catalog.removeOne') }}
        </button>
        <button
          type="button"
          class="more"
          :aria-label="t('catalog.addOne')"
          @click="$emit('changeQuantity', entry.index, entry.line.quantity + 1)"
        >
          {{ t('catalog.addOne') }}
        </button>
        <span v-if="stationLabelFor(entry.line) !== null" class="line-station">
          {{ stationLabelFor(entry.line) }}
        </span>
        <button
          v-if="entry.line.candidateLocationIds.length > 1"
          type="button"
          class="change-station"
          @click="$emit('changeStation', entry.index)"
        >
          {{ t('line.changeStation') }}
        </button>
        <label class="line-note">
          <span>{{ t('catalog.lineNote') }}</span>
          <input
            type="text"
            :value="entry.line.note ?? ''"
            :placeholder="t('catalog.lineNotePlaceholder')"
            @input="$emit('changeNote', entry.index, noteInput($event))"
          />
        </label>
        <p v-if="entry.line.isNoLongerOnTheMenu" class="no-longer-on-the-menu">
          {{ t('catalog.lineNoLongerOnTheMenu') }}
        </p>
        <p v-else-if="entry.line.isSoldOut" class="sold-out">
          {{ t('catalog.itemSoldOut', { name: entry.line.name }) }}
        </p>
      </div>
    </section>
  </div>
</template>
