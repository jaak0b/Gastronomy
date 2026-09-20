<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationOrder } from '../../shared/api/apiTypes'
import {
  itemLineText,
  itemLines,
  openItemsIn,
  selectedOpenItemIds,
} from '../../shared/core/stationBoard'
import { useStationOrderHeader } from '../composables/useStationOrderHeader'
import BaseStationCard from './BaseStationCard.vue'

const props = defineProps<{
  stationOrder: StationOrder
  selectedItemIds: string[]
  isWorking: boolean
  showHide: boolean
}>()
const emit = defineEmits<{
  toggleItem: [orderItemId: string]
  fulfill: [stationOrder: StationOrder, orderItemIds: string[]]
  hide: [stationOrderId: string]
}>()

const { t } = useI18n()
const { deliveryText, deliveryModeColour, orderReference, doneCounter } = useStationOrderHeader(
  () => props.stationOrder,
)

const isGrouped = ref(false)
const openItems = computed(() => openItemsIn(props.stationOrder))
const selectedHere = computed(() =>
  selectedOpenItemIds(props.stationOrder, props.selectedItemIds),
)
const groupedLines = computed(() => itemLines(openItems.value))
const viewToggleLabel = computed(() =>
  t(isGrouped.value ? 'station.listView' : 'station.groupedView'),
)
const viewToggleIcon = computed(() =>
  isGrouped.value ? 'mdi-format-list-bulleted' : 'mdi-format-list-group',
)

function isSelected(orderItemId: string): boolean {
  return selectedHere.value.includes(orderItemId)
}
</script>

<template>
  <BaseStationCard class="station-order mb-4 bg-surface" :delivery-mode-colour="deliveryModeColour">
    <div class="station-order-head d-flex flex-wrap align-baseline ga-2">
      <span class="table-name text-h5">
        {{ t('station.tableIs', { name: stationOrder.tableName }) }}
      </span>
      <span class="station-order-heading text-body-2 text-medium-emphasis">
        {{ orderReference }}
      </span>
      <span class="done-counter text-body-2 ms-auto">{{ doneCounter }}</span>
    </div>
    <div class="station-order-mode-row d-flex flex-wrap align-center ga-2 mb-1">
      <span class="delivery-mode text-body-1 font-weight-medium" :style="{ color: deliveryModeColour }">
        {{ deliveryText }}
      </span>
      <v-btn
        class="grouped-toggle ms-auto"
        variant="outlined"
        color="primary"
        :prepend-icon="viewToggleIcon"
        :aria-pressed="isGrouped"
        @click="isGrouped = !isGrouped"
      >
        {{ viewToggleLabel }}
      </v-btn>
    </div>
    <v-divider class="my-2" />
    <template v-if="!isGrouped">
      <v-btn
        v-for="item in openItems"
        :key="item.orderItemId"
        class="station-item"
        :class="{ selected: isSelected(item.orderItemId) }"
        variant="tonal"
        block
        size="large"
        :aria-pressed="isSelected(item.orderItemId)"
        @click="emit('toggleItem', item.orderItemId)"
      >
        <span class="item-text">
          <span class="item-name">{{ item.itemName }}</span>
          <span v-if="item.note !== null" class="item-note text-body-2">
            <v-icon class="item-note-icon" icon="mdi-note-text-outline" size="small" />
            {{ t('station.note', { note: item.note }) }}
          </span>
        </span>
        <v-icon v-if="isSelected(item.orderItemId)" class="selected-tick ms-auto" icon="mdi-check" />
      </v-btn>
    </template>
    <div v-else class="grouped-items">
      <p v-for="line in groupedLines" :key="line.key" class="grouped-line text-body-1 mb-0">
        {{ itemLineText(line, t) }}
      </p>
    </div>
    <div v-if="!isGrouped" class="card-actions d-flex mt-3">
      <v-btn
        class="fulfill"
        color="primary"
        variant="flat"
        size="large"
        :disabled="selectedHere.length === 0 || isWorking"
        @click="emit('fulfill', stationOrder, selectedHere)"
      >
        {{ t('station.done') }}
      </v-btn>
      <v-btn
        v-if="showHide"
        class="hide"
        variant="outlined"
        size="large"
        :disabled="isWorking"
        @click="emit('hide', stationOrder.stationOrderId)"
      >
        {{ t('station.hideHere') }}
      </v-btn>
    </div>
  </BaseStationCard>
</template>

<style scoped>
.station-item {
  justify-content: flex-start;
  height: auto;
  min-height: 3rem;
  padding-block: 0.375rem;
  text-transform: none;
  letter-spacing: normal;
}

.station-item :deep(.v-btn__content) {
  flex: 1 1 auto;
  justify-content: flex-start;
  white-space: normal;
}

.station-item + .station-item {
  margin-top: 0.5rem;
}

.station-item.selected {
  outline: 3px solid rgb(var(--v-theme-primary));
  outline-offset: -3px;
}

.station-item .item-text {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  min-width: 0;
  text-align: start;
}

.station-item .item-name {
  overflow-wrap: anywhere;
}

.station-item .item-note {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  margin-block-start: 0.125rem;
  color: rgb(var(--v-theme-on-surface));
  font-weight: 700;
  overflow-wrap: anywhere;
}

.station-item .selected-tick {
  flex: 0 0 auto;
}

.grouped-items {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.grouped-line {
  overflow-wrap: anywhere;
}

.card-actions {
  gap: 0.75rem;
}

.card-actions .v-btn {
  flex: 1 1 auto;
  min-width: 0;
}
</style>
