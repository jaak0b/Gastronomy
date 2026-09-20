<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationOrder } from '../../shared/api/apiTypes'
import { isFulfilled, itemLineText, itemLines } from '../../shared/core/stationBoard'
import { useStationOrderHeader } from '../composables/useStationOrderHeader'
import BaseStationCard from './BaseStationCard.vue'

const props = defineProps<{ stationOrder: StationOrder; isWorking: boolean }>()
const emit = defineEmits<{ putBack: [orderItemId: string] }>()

const { t } = useI18n()
const { deliveryText, deliveryModeColour, orderReference, takenByText, doneCounter } =
  useStationOrderHeader(() => props.stationOrder)

const unitSummary = computed(() =>
  itemLines(props.stationOrder.items)
    .map((line) => itemLineText(line, t))
    .join(t('station.unitSeparator')),
)
</script>

<template>
  <BaseStationCard class="station-fulfilled mb-4 bg-surface" :delivery-mode-colour="deliveryModeColour">
    <div class="station-order-head d-flex flex-wrap align-baseline ga-2">
      <span class="table-name text-h5">
        {{ t('station.tableIs', { name: stationOrder.tableName }) }}
      </span>
      <span class="station-order-heading text-body-2 text-medium-emphasis">
        {{ orderReference }}
      </span>
      <span class="taken-by text-body-2 text-medium-emphasis">
        {{ takenByText }}
      </span>
      <span class="done-counter text-body-2 ms-auto">{{ doneCounter }}</span>
    </div>
    <div class="station-order-mode-row d-flex flex-wrap align-baseline ga-2 mb-1">
      <span class="delivery-mode text-body-1 font-weight-medium" :style="{ color: deliveryModeColour }">
        {{ deliveryText }}
      </span>
      <span v-if="unitSummary !== ''" class="unit-summary text-body-1">{{ unitSummary }}</span>
    </div>
    <v-divider class="my-2" />
    <div
      v-for="item in stationOrder.items"
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
  </BaseStationCard>
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
