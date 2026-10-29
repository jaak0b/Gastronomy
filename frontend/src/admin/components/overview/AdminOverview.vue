<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminStationsStore } from '../../stores/stations'
import { useAdminItemsStore } from '../../stores/items'
import { useAdminCategoriesStore } from '../../stores/categories'
import { useAdminFestivalsStore } from '../../stores/festivals'
import { combineReleases } from '../../../shared/core/combineReleases'

const { t } = useI18n()
const stations = useAdminStationsStore()
const items = useAdminItemsStore()
const categories = useAdminCategoriesStore()
const festivals = useAdminFestivalsStore()

interface ReadinessRow {
  key: string
  parameters: Record<string, string | number>
  count: number | null
}

let stopListening: (() => void) | null = null

const noFestivalExists = computed(() => festivals.shownFestivals.length === 0)
const runningFestival = computed(() => festivals.runningFestival)

const rows = computed<ReadinessRow[]>(() => {
  const readiness: ReadinessRow[] = []
  if (runningFestival.value === null) {
    return readiness
  }
  if (stations.stations.every((station) => !station.isAtFestival)) {
    readiness.push({ key: 'admin.overview.messages.missingStation', parameters: {}, count: null })
  }
  if (categories.categories.length === 0) {
    readiness.push({ key: 'admin.overview.messages.missingCategory', parameters: {}, count: null })
  }
  const atTheFestival = items.items.filter((item) => item.atTheFestival !== null)
  if (atTheFestival.length === 0) {
    readiness.push({ key: 'admin.overview.messages.missingItems', parameters: {}, count: null })
  }
  const withoutStation = atTheFestival.filter(
    (item) => (item.atTheFestival?.stationIds.length ?? 0) === 0,
  ).length
  if (withoutStation > 0) {
    readiness.push({
      key: 'admin.overview.messages.itemsWithoutStation',
      parameters: { count: withoutStation },
      count: withoutStation,
    })
  }
  return readiness
})

onMounted(async () => {
  stopListening = combineReleases(
    festivals.listen(),
    stations.listen(),
    categories.listen(),
    items.listen(),
  )
  await festivals.load()
  const festival = festivals.runningFestival
  if (festival === null) {
    return
  }
  await stations.loadAtTheFestival(festival.festivalId)
  await categories.load()
  await items.loadAtTheFestival(festival.festivalId)
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="admin-overview">
    <h1 class="app-page-title admin-section-gap">{{ t('admin.overview.title') }}</h1>
    <v-alert v-if="noFestivalExists" class="missing-festival admin-section-gap" data-test="missing-festival" type="warning" variant="tonal">
      {{ t('admin.overview.messages.missingFestival') }}
    </v-alert>
    <v-alert
      v-else-if="runningFestival === null"
      class="not-running admin-section-gap"
      data-test="not-running"
      type="info"
      variant="tonal"
    >
      {{ t('admin.overview.messages.noFestivalIsRunning') }}
    </v-alert>
    <template v-else>
      <h2 class="running-festival app-section-title mb-2" data-test="running-festival">{{ runningFestival.name }}</h2>
      <v-alert v-if="rows.length === 0" class="ready admin-section-gap" type="success" variant="tonal">
        {{ t('admin.overview.messages.ready') }}
      </v-alert>
      <v-alert
        v-for="(row, index) in rows"
        :key="index"
        class="readiness-row mb-2"
        data-test="readiness-row"
        type="warning"
        variant="tonal"
      >
        {{ row.count === null ? t(row.key, row.parameters) : t(row.key, row.parameters, row.count) }}
      </v-alert>
    </template>
  </v-container>
</template>
