<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminStationsStore } from '../../../stores/admin/stations'
import { useAdminItemsStore } from '../../../stores/admin/items'
import { useAdminCategoriesStore } from '../../../stores/admin/categories'
import { useAdminFestivalsStore } from '../../../stores/admin/festivals'

const { t } = useI18n()
const phoneAddress = window.location.origin
const stations = useAdminStationsStore()
const items = useAdminItemsStore()
const categories = useAdminCategoriesStore()
const festivals = useAdminFestivalsStore()

interface ReadinessRow {
  key: string
  parameters: Record<string, string | number>
  count: number | null
}

const noFestivalExists = computed(() => festivals.shownFestivals.length === 0)
const pickedFestival = computed(() => festivals.pickedFestival)

const rows = computed<ReadinessRow[]>(() => {
  const readiness: ReadinessRow[] = []
  if (pickedFestival.value === null) {
    return readiness
  }
  if (stations.stations.every((station) => !station.isAtTheFestival)) {
    readiness.push({ key: 'admin.overview.missingStation', parameters: {}, count: null })
  }
  if (categories.categories.length === 0) {
    readiness.push({ key: 'admin.overview.missingCategory', parameters: {}, count: null })
  }
  const onTheMenu = items.items.filter((item) => item.atTheFestival !== null)
  if (onTheMenu.length === 0) {
    readiness.push({ key: 'admin.overview.missingItems', parameters: {}, count: null })
  }
  const withoutStation = onTheMenu.filter(
    (item) => (item.atTheFestival?.stationIds.length ?? 0) === 0,
  ).length
  if (withoutStation > 0) {
    readiness.push({
      key: 'admin.overview.itemsWithoutStation',
      parameters: { count: withoutStation },
      count: withoutStation,
    })
  }
  for (const station of stations.stations) {
    if (station.isAtTheFestival && station.isActive && !station.hasDevice) {
      readiness.push({
        key: 'admin.overview.stationWithoutTablet',
        parameters: { name: station.name },
        count: null,
      })
    }
  }
  return readiness
})

onMounted(async () => {
  await festivals.load()
  await stations.load()
  await categories.load()
  await items.load()
})
</script>

<template>
  <v-container class="admin-overview">
    <h1 class="text-h5 mb-4">{{ t('admin.overview.title') }}</h1>
    <v-alert v-if="noFestivalExists" class="missing-festival mb-4" type="warning" variant="tonal">
      {{ t('admin.overview.missingFestival') }}
    </v-alert>
    <v-alert
      v-else-if="pickedFestival === null"
      class="no-festival-picked mb-4"
      type="warning"
      variant="tonal"
    >
      {{ t('admin.overview.noFestivalPicked') }}
    </v-alert>
    <template v-else>
      <h2 class="picked-festival text-h6 mb-2">{{ pickedFestival.name }}</h2>
      <v-alert
        v-if="!pickedFestival.isRunning"
        class="not-running mb-4"
        type="info"
        variant="tonal"
      >
        {{ t('admin.overview.noFestivalIsRunning') }}
      </v-alert>
      <v-alert v-if="rows.length === 0" class="ready mb-4" type="success" variant="tonal">
        {{ t('admin.overview.ready') }}
      </v-alert>
      <v-alert
        v-for="(row, index) in rows"
        :key="index"
        class="readiness-row mb-2"
        type="warning"
        variant="tonal"
      >
        {{ row.count === null ? t(row.key, row.parameters) : t(row.key, row.parameters, row.count) }}
      </v-alert>
    </template>
    <p class="phone-address mt-4">{{ t('admin.overview.address', { url: phoneAddress }) }}</p>
  </v-container>
</template>
