<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { AdminStation } from '../../../stores/admin/stations'

const props = defineProps<{
  itemName: string
  stations: AdminStation[]
  selectedStationIds: string[]
}>()
const emit = defineEmits<{ toggle: [stationId: string] }>()

const { t } = useI18n()

</script>

<template>
  <v-sheet class="assignment-editor pa-4 mb-4" border rounded>
    <div class="text-subtitle-1">{{ t('admin.assignment.title') }}</div>
    <v-checkbox
      v-for="station in stations"
      :key="station.stationId"
      :label="station.name"
      :model-value="selectedStationIds.includes(station.stationId)"
      @update:model-value="emit('toggle', station.stationId)"
    />
  </v-sheet>
</template>
