<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminStationsStore } from '../../../stores/admin/stations'
import { useAdminItemsStore } from '../../../stores/admin/items'
import { request } from '../../../api/client'

const { t } = useI18n()
const phoneAddress = window.location.origin
const stations = useAdminStationsStore()
const items = useAdminItemsStore()

interface ReadinessRow {
  key: string
  parameters: Record<string, string | number>
  count: number | null
}

const rows = computed<ReadinessRow[]>(() => {
  const readiness: ReadinessRow[] = []
  if (stations.stations.length === 0) {
    readiness.push({ key: 'admin.overview.missingStation', parameters: {}, count: null })
  }
  if (items.items.length === 0) {
    readiness.push({ key: 'admin.overview.missingItems', parameters: {}, count: null })
  }
  const withoutStation = items.items.filter((item) => item.stationIds.length === 0).length
  if (withoutStation > 0) {
    readiness.push({
      key: 'admin.overview.itemsWithoutStation',
      parameters: { count: withoutStation },
      count: withoutStation,
    })
  }
  for (const station of stations.stations) {
    if (station.isActive && !station.hasDevice) {
      readiness.push({
        key: 'admin.overview.stationWithoutTablet',
        parameters: { name: station.name },
        count: null,
      })
    }
  }
  return readiness
})

const isConfirmingReset = ref(false)
const resetDoneText = ref<string | null>(null)
const resetFailed = ref(false)

function askToReset(): void {
  resetDoneText.value = null
  resetFailed.value = false
  isConfirmingReset.value = true
}

function cancelReset(): void {
  isConfirmingReset.value = false
}

async function confirmReset(): Promise<void> {
  isConfirmingReset.value = false
  const result = await request('/api/admin/numbers/reset', { method: 'POST' })
  if (result.kind === 'ok') {
    resetDoneText.value = t('admin.numbers.done')
    resetFailed.value = false
    return
  }

  resetDoneText.value = null
  resetFailed.value = true
}

onMounted(async () => {
  await stations.load()
  await items.load()
})
</script>

<template>
  <v-container class="admin-overview">
    <h1 class="text-h5 mb-4">{{ t('admin.overview.title') }}</h1>
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
    <p class="phone-address mt-4">{{ t('admin.overview.address', { url: phoneAddress }) }}</p>

    <v-card class="numbers-reset mt-6">
      <v-card-title>{{ t('admin.numbers.title') }}</v-card-title>
      <v-card-text>
        <p class="numbers-help text-medium-emphasis">{{ t('admin.numbers.help') }}</p>
        <template v-if="isConfirmingReset">
          <p class="confirm-question mt-2">{{ t('admin.numbers.confirm') }}</p>
        </template>
        <v-alert v-if="resetDoneText !== null" class="reset-done mt-2" type="success" variant="tonal">
          {{ resetDoneText }}
        </v-alert>
        <v-alert v-if="resetFailed" class="reset-failed mt-2" type="error" variant="tonal">
          {{ t('admin.loadFailed') }}
        </v-alert>
      </v-card-text>
      <v-card-actions>
        <v-btn v-if="!isConfirmingReset" class="secondary" variant="text" @click="askToReset()">
          {{ t('admin.numbers.reset') }}
        </v-btn>
        <template v-else>
          <v-btn class="primary" color="primary" @click="confirmReset()">
            {{ t('admin.numbers.confirmYes') }}
          </v-btn>
          <v-btn class="secondary" variant="text" @click="cancelReset()">
            {{ t('admin.numbers.confirmNo') }}
          </v-btn>
        </template>
      </v-card-actions>
    </v-card>
  </v-container>
</template>
