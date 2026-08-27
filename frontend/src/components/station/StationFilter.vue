<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { StationLocation } from '../../stores/station'

defineProps<{ locations: StationLocation[]; selectedLocationId: string | null }>()
defineEmits<{ select: [locationId: string] }>()

const { t } = useI18n()
</script>

<template>
  <div class="station-filter">
    <v-select
      class="station-select"
      :label="t('station.filterLabel')"
      :model-value="selectedLocationId"
      :items="locations.map((location) => ({ title: location.name, value: location.locationId }))"
      @update:model-value="$emit('select', $event)"
    />
    <p class="help text-medium-emphasis">{{ t('station.filterHelp') }}</p>
  </div>
</template>
