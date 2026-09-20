<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminStation } from '../../../shared/api/apiTypes'

const props = defineProps<{
  stations: AdminStation[]
  selectedStationIds: string[]
  errorText: string | null
}>()
const emit = defineEmits<{ select: [stationIds: string[]] }>()

const { t } = useI18n()

const summary = computed(() => {
  if (props.selectedStationIds.length === 0) {
    return t('admin.festival.chooseStation')
  }
  return props.stations
    .filter((station) => props.selectedStationIds.includes(station.stationId))
    .map((station) => station.name)
    .join(', ')
})

const hasNoStation = computed(() => props.selectedStationIds.length === 0)

function toggle(stationId: string): void {
  emit(
    'select',
    props.selectedStationIds.includes(stationId)
      ? props.selectedStationIds.filter((id) => id !== stationId)
      : [...props.selectedStationIds, stationId],
  )
}
</script>

<template>
  <div class="station-select-field">
    <v-menu :close-on-content-click="false">
      <template #activator="{ props: activatorProps }">
        <v-btn
          v-bind="activatorProps"
          class="station-select"
          variant="outlined"
          :color="hasNoStation ? 'error' : undefined"
        >
          {{ summary }}
        </v-btn>
      </template>
      <v-list class="station-options">
        <v-list-item v-for="station in stations" :key="station.stationId">
          <v-checkbox
            class="station-checkbox"
            :model-value="selectedStationIds.includes(station.stationId)"
            :label="station.name"
            hide-details
            @update:model-value="toggle(station.stationId)"
          />
        </v-list-item>
      </v-list>
    </v-menu>
    <p v-if="errorText !== null" class="station-select-error text-error text-body-2 mt-1">
      {{ errorText }}
    </p>
  </div>
</template>
