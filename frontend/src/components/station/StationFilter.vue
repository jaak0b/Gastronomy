<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import type { Station } from '../../stores/station'

defineProps<{ stations: Station[]; selectedStationId: string | null }>()
defineEmits<{ select: [stationId: string] }>()

const { t } = useI18n()
</script>

<template>
  <div class="station-filter">
    <v-select
      class="station-select"
      :label="t('station.filterLabel')"
      :model-value="selectedStationId"
      :items="stations.map((station) => ({ title: station.name, value: station.stationId }))"
      @update:model-value="$emit('select', $event)"
    />
    <p class="help text-medium-emphasis">{{ t('station.filterHelp') }}</p>
  </div>
</template>
