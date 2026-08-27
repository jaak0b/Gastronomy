<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useAdminLocationsStore } from '../../../stores/admin/locations'
import { useAdminItemsStore } from '../../../stores/admin/items'
import { useAdminPrintersStore } from '../../../stores/admin/printers'
import { request } from '../../../api/client'

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

    <div class="numbers-reset">
      <h2>{{ t('admin.numbers.title') }}</h2>
      <p class="numbers-help">{{ t('admin.numbers.help') }}</p>
      <button v-if="!isConfirmingReset" type="button" class="secondary" @click="askToReset()">
        {{ t('admin.numbers.reset') }}
      </button>
      <div v-else class="confirm-block">
        <p class="confirm-question">{{ t('admin.numbers.confirm') }}</p>
        <button type="button" class="primary" @click="confirmReset()">
          {{ t('admin.numbers.confirmYes') }}
        </button>
        <button type="button" class="secondary" @click="cancelReset()">
          {{ t('admin.numbers.confirmNo') }}
        </button>
      </div>
      <p v-if="resetDoneText !== null" class="reset-done">{{ resetDoneText }}</p>
      <p v-if="resetFailed" class="reset-failed">{{ t('admin.loadFailed') }}</p>
    </div>
  </section>
</template>
