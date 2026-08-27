<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { StationLocation } from '../../stores/station'

defineProps<{ locations: StationLocation[]; selectedLocationId: string | null }>()
defineEmits<{ select: [locationId: string] }>()

const { t } = useI18n()
</script>

<template>
  <div class="station-filter">
    <label>
      <span>{{ t('station.filterLabel') }}</span>
      <select
        :value="selectedLocationId"
        @change="$emit('select', ($event.target as HTMLSelectElement).value)"
      >
        <option
          v-for="location in locations"
          :key="location.locationId"
          :value="location.locationId"
        >
          {{ location.name }}
        </option>
      </select>
    </label>
    <p class="help">{{ t('station.filterHelp') }}</p>
  </div>
</template>
