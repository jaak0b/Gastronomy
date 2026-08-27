<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminLocationsStore } from '../../../stores/admin/locations'
import { useAdminItemsStore } from '../../../stores/admin/items'
import { useAdminPrintersStore } from '../../../stores/admin/printers'

const { t } = useI18n()
const phoneAddress = window.location.origin
const locations = useAdminLocationsStore()
const items = useAdminItemsStore()
const printers = useAdminPrintersStore()

interface ReadinessRow {
  key: string
  parameters: Record<string, string | number>
  count: number | null
}

const rows = computed<ReadinessRow[]>(() => {
  const readiness: ReadinessRow[] = []
  if (locations.locations.length === 0) {
    readiness.push({ key: 'admin.overview.missingLocation', parameters: {}, count: null })
  }
  if (items.items.length === 0) {
    readiness.push({ key: 'admin.overview.missingItems', parameters: {}, count: null })
  }
  const withoutLocation = items.items.filter((item) => item.locationIds.length === 0).length
  if (withoutLocation > 0) {
    readiness.push({
      key: 'admin.overview.itemsWithoutLocation',
      parameters: { count: withoutLocation },
      count: withoutLocation,
    })
  }
  for (const printer of printers.printers) {
    if (printer.transport === 'Mock') {
      readiness.push({
        key: 'admin.overview.missingPrinter',
        parameters: { name: printer.locationName },
        count: null,
      })
    }
    if (printer.isPaperNearEnd) {
      readiness.push({
        key: 'admin.overview.paperNearEnd',
        parameters: { name: printer.locationName },
        count: null,
      })
    }
  }
  return readiness
})

onMounted(async () => {
  await locations.load()
  await items.load()
  await printers.load()
})
</script>

<template>
  <section class="admin-overview">
    <h1>{{ t('admin.overview.title') }}</h1>
    <p v-if="rows.length === 0" class="ready">{{ t('admin.overview.ready') }}</p>
    <p v-for="(row, index) in rows" :key="index" class="readiness-row">
      {{ row.count === null ? t(row.key, row.parameters) : t(row.key, row.parameters, row.count) }}
    </p>
    <p class="phone-address">{{ t('admin.overview.address', { url: phoneAddress }) }}</p>
  </section>
</template>
