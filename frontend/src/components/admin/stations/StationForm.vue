<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminStation } from '../../../stores/admin/stations'
import { useAdminPrintersStore } from '../../../stores/admin/printers'

const props = defineProps<{ station: AdminStation | null }>()
const emit = defineEmits<{
  save: [value: { stationId?: string; name: string; sortOrder: number; printerId: string | null }]
}>()

const { t } = useI18n()
const printers = useAdminPrintersStore()
const name = ref(props.station?.name ?? '')
const sortOrder = ref(props.station?.sortOrder ?? 1)
const printerId = ref<string | null>(props.station?.printerId ?? null)

const printerChoices = computed(() => [
  { title: t('admin.stations.noPrinter'), value: null },
  ...printers.printers.map((printer) => ({ title: printer.name, value: printer.printerId })),
])

function save(): void {
  emit('save', {
    stationId: props.station?.stationId,
    name: name.value,
    sortOrder: sortOrder.value,
    printerId: printerId.value,
  })
}

onMounted(printers.load)
</script>

<template>
  <v-form class="station-form pa-4" @submit.prevent="save">
    <v-text-field v-model="name" class="mb-4" :label="t('admin.stations.title')" />
    <v-select
      v-model="printerId"
      class="printer-field mb-4"
      :label="t('admin.stations.printerLabel')"
      :items="printerChoices"
    />
    <v-btn type="submit" color="primary" :disabled="name.trim().length === 0">
      {{ t('admin.save') }}
    </v-btn>
  </v-form>
</template>
