<script setup lang="ts">
import type { AdminStation } from '../../../stores/admin/stations'

defineProps<{
  stations: AdminStation[]
  selectedStationIds: string[]
}>()
const emit = defineEmits<{ toggle: [stationId: string] }>()
</script>

<template>
  <div class="station-chips d-flex flex-wrap ga-2">
    <v-chip
      v-for="station in stations"
      :key="station.stationId"
      class="station-chip"
      :class="{ 'is-selected': selectedStationIds.includes(station.stationId) }"
      size="small"
      :color="selectedStationIds.includes(station.stationId) ? 'primary' : undefined"
      :variant="selectedStationIds.includes(station.stationId) ? 'flat' : 'outlined'"
      @click="emit('toggle', station.stationId)"
    >
      {{ station.name }}
    </v-chip>
  </div>
</template>
