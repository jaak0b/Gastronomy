<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationSlice } from '../../core/apiTypes'
import {
  deliveryModeColour,
  deliveryModeKey,
  itemLineText,
  itemLines,
  openItemsOf,
  selectedOpenItemIds,
  type ItemLine,
} from '../../core/stationBoard'

const props = defineProps<{
  slice: StationSlice
  selectedItemIds: string[]
  isWorking: boolean
  showHide: boolean
}>()
const emit = defineEmits<{
  toggleItem: [orderItemId: string]
  fulfil: [slice: StationSlice, orderItemIds: string[]]
  hide: [stationOrderId: string]
}>()

const { t } = useI18n()

const isGrouped = ref(false)
const openItems = computed(() => openItemsOf(props.slice))
const selectedHere = computed(() => selectedOpenItemIds(props.slice, props.selectedItemIds))
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
const groupedLines = computed(() => itemLines(openItems.value))
const viewToggleLabel = computed(() =>
  t(isGrouped.value ? 'station.listView' : 'station.groupedView'),
)

function isSelected(orderItemId: string): boolean {
  return selectedHere.value.includes(orderItemId)
}

function lineText(line: ItemLine): string {
  return itemLineText(line, t)
}
</script>

<template>
  <v-card class="station-slice mb-4" :style="{ borderColor: modeColour }" variant="outlined">
    <v-card-text class="pa-3">
      <div class="slice-head d-flex align-baseline ga-2">
        <span class="table-name text-h5">{{ t('station.tableIs', { name: slice.tableName }) }}</span>
        <span class="delivery-mode text-body-1 font-weight-medium" :style="{ color: modeColour }">
          {{ deliveryText }}
        </span>
        <span class="slice-heading text-body-1 text-medium-emphasis">{{ orderReference }}</span>
        <span class="done-counter text-body-1 ms-auto">{{ doneCounter }}</span>
        <v-btn
          class="grouped-toggle"
          variant="text"
          color="primary"
          :aria-pressed="isGrouped"
          @click="isGrouped = !isGrouped"
        >
          {{ viewToggleLabel }}
        </v-btn>
      </div>
      <p v-if="slice.note !== null" class="slice-note text-body-1 mt-1 mb-0">
        {{ t('station.orderNote', { note: slice.note }) }}
      </p>
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
          <v-icon
            v-if="isSelected(item.orderItemId)"
            class="selected-tick ms-auto"
            icon="mdi-check"
          />
        </v-btn>
      </template>
      <div v-else class="grouped-items">
        <p
          v-for="(line, position) in groupedLines"
          :key="position"
          class="grouped-line text-body-1 mb-0"
        >
          {{ lineText(line) }}
        </p>
      </div>
    </v-card-text>
    <v-card-actions v-if="!isGrouped" class="card-actions">
      <v-btn
        class="fulfill"
        color="primary"
        variant="flat"
        size="large"
        :disabled="selectedHere.length === 0 || isWorking"
        @click="emit('fulfil', slice, selectedHere)"
      >
        {{ t('station.done') }}
      </v-btn>
      <v-btn
        v-if="showHide"
        class="hide"
        variant="outlined"
        size="large"
        :disabled="isWorking"
        @click="emit('hide', slice.stationOrderId)"
      >
        {{ t('station.hideHere') }}
      </v-btn>
    </v-card-actions>
  </v-card>
</template>

<style scoped>
.station-slice {
  border-width: 3px;
  border-style: solid;
}

.slice-head {
  flex-wrap: wrap;
}

.slice-head > span {
  min-width: 0;
}

.slice-head .table-name {
  overflow-wrap: anywhere;
}

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
