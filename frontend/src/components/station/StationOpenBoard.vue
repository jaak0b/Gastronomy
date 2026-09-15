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
  <section class="station-open-board">
    <div class="board-head d-flex align-center ga-3 mb-4">
      <h2 class="board-heading text-h5 flex-grow-1">{{ t('station.openArticlesHeading') }}</h2>
      <v-btn class="back-to-orders" variant="outlined" size="large" @click="emit('close')">
        {{ t('station.backToOrders') }}
      </v-btn>
    </div>
    <v-alert v-if="failureText !== null" class="board-failed mb-4" type="warning" variant="tonal">
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
  </section>
</template>

<style scoped>
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
</style>
