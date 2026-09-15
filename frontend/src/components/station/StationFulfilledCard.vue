<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationSlice } from '../../core/apiTypes'
import {
  deliveryModeColour,
  deliveryModeKey,
  isFulfilled,
  itemLineText,
  itemLines,
} from '../../core/stationBoard'
import './stationCard.css'

const props = defineProps<{ slice: StationSlice; isWorking: boolean }>()
const emit = defineEmits<{ putBack: [orderItemId: string] }>()

const { t } = useI18n()

const deliveryText = computed(() => t(deliveryModeKey(props.slice.deliveryMode)))
const modeColour = computed(
  () => `rgb(var(--v-theme-${deliveryModeColour(props.slice.deliveryMode)}))`,
)
const orderReference = computed(() =>
  t('station.order', {
    order: props.slice.globalOrderNumber,
    sequence: props.slice.stationOrderNumber,
  }),
)
const doneCounter = computed(() =>
  t('station.doneCounter', {
    fulfilled: props.slice.fulfilledItemCount,
    total: props.slice.itemCount,
  }),
)
const unitSummary = computed(() =>
  itemLines(props.slice.items)
    .map((line) => itemLineText(line, t))
    .join(t('station.unitSeparator')),
)
</script>

<template>
  <fieldset class="station-fulfilled mb-4 bg-surface" :style="{ borderColor: modeColour }">
    <legend class="slice-legend d-flex flex-wrap align-baseline ga-2">
      <span class="table-name text-h5">{{ t('station.tableIs', { name: slice.tableName }) }}</span>
      <span class="slice-heading text-body-2 text-medium-emphasis">{{ orderReference }}</span>
      <span class="done-counter text-body-2 ms-auto">{{ doneCounter }}</span>
    </legend>
    <div class="slice-mode-row d-flex flex-wrap align-baseline ga-2 mb-1">
      <span class="delivery-mode text-body-1 font-weight-medium" :style="{ color: modeColour }">
        {{ deliveryText }}
      </span>
      <span v-if="unitSummary !== ''" class="unit-summary text-body-1">{{ unitSummary }}</span>
    </div>
    <p v-if="slice.note !== null" class="slice-note text-body-1 mt-1 mb-0">
      {{ t('station.orderNote', { note: slice.note }) }}
    </p>
    <v-divider class="my-2" />
    <div
      v-for="item in slice.items"
      :key="item.orderItemId"
      class="station-item d-flex align-center ga-3"
      :class="{ fulfilled: isFulfilled(item) }"
    >
      <v-icon v-if="isFulfilled(item)" class="item-tick" icon="mdi-check" />
      <div class="item-text flex-grow-1">
        <div class="item-name text-body-1">{{ item.itemName }}</div>
        <div v-if="item.note !== null" class="item-note text-body-2">
          {{ t('station.note', { note: item.note }) }}
        </div>
      </div>
      <v-btn
        v-if="isFulfilled(item)"
        class="put-back"
        variant="outlined"
        size="large"
        :disabled="isWorking"
        @click="emit('putBack', item.orderItemId)"
      >
        {{ t('station.putBack') }}
      </v-btn>
    </div>
  </fieldset>
</template>

<style scoped>
.station-item {
  min-height: 3rem;
  padding-block: 0.375rem;
}

.station-item .item-text {
  min-width: 0;
  overflow-wrap: anywhere;
}

.station-item.fulfilled .item-name {
  color: rgba(var(--v-theme-on-surface), var(--v-medium-emphasis-opacity));
}

.put-back {
  flex: 0 0 auto;
}
</style>
