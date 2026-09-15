<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  deliveryModeColour,
  itemLineText,
  linesByCount,
  type ItemLine,
} from '../../core/stationBoard'

const props = defineProps<{
  togetherCount: number
  asItComesCount: number
  lines: ItemLine[]
  failureText: string | null
}>()
const emit = defineEmits<{ close: [] }>()

const { t } = useI18n()

const orderedLines = computed(() => linesByCount(props.lines))
</script>

<template>
  <v-dialog class="station-open-board" model-value persistent fullscreen>
    <v-card class="card d-flex flex-column">
      <v-card-title class="title">{{ t('station.overviewHeading') }}</v-card-title>
      <v-card-text class="body flex-grow-1">
        <v-alert
          v-if="failureText !== null"
          class="board-failed mb-4"
          type="warning"
          variant="tonal"
        >
          {{ failureText }}
        </v-alert>
        <div class="mode-counts d-flex flex-wrap ga-2">
          <v-chip
            class="stat-together"
            :color="deliveryModeColour('together')"
            variant="flat"
            size="large"
          >
            {{ t('station.statsTogetherChip', { count: togetherCount }) }}
          </v-chip>
          <v-chip
            class="stat-as-it-comes"
            :color="deliveryModeColour('asItComes')"
            variant="flat"
            size="large"
          >
            {{ t('station.statsAsItComesChip', { count: asItComesCount }) }}
          </v-chip>
        </div>
        <ul class="units">
          <li v-for="(line, position) in orderedLines" :key="position" class="unit">
            {{ itemLineText(line, t) }}
          </li>
        </ul>
      </v-card-text>
      <v-card-actions class="actions justify-end">
        <v-btn class="close" variant="outlined" size="large" @click="emit('close')">
          {{ t('station.close') }}
        </v-btn>
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.card {
  height: 100%;
  border-radius: 0;
}

.title {
  white-space: normal;
  overflow: visible;
  text-overflow: clip;
  line-height: 1.35;
  padding-block: 1rem;
}

.body {
  padding: 1rem;
  overflow-y: auto;
}

.units {
  margin: 1.25rem 0 0;
  padding: 0;
  list-style: none;
  font-size: 1.5rem;
  line-height: 1.35;
}

.unit + .unit {
  margin-top: 0.75rem;
}

.actions {
  padding: 0 1rem 1rem;
}

.actions .v-btn {
  min-width: 0;
  height: auto;
  min-height: 3.5rem;
  padding-block: 0.75rem;
  text-transform: none;
  letter-spacing: normal;
}
</style>
