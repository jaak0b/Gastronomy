<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { StationOrderQueueView } from '../../shared/api/generatedSchemas'
import { isCancelled } from '../../shared/core/orderItemStates'
import {
  cancellationText,
  itemLineText,
  itemLines,
  itemsNotHandedOutIn,
  selectedItemIdsAwaitingAcknowledgement,
  selectedOpenItemIds,
} from '../../shared/core/stationBoard'
import { useStationOrderHeader } from '../composables/useStationOrderHeader'
import BaseStationCard from './BaseStationCard.vue'

const props = defineProps<{
  stationOrder: StationOrderQueueView
  selectedItemIds: string[]
  isWorking: boolean
  showHide: boolean
}>()
const emit = defineEmits<{
  toggleItem: [orderItemId: string]
  fulfill: [stationOrder: StationOrderQueueView, orderItemIds: string[]]
  acknowledge: [orderItemIds: string[]]
  hide: [stationOrderId: string]
}>()

const { t, locale } = useI18n()
const { deliveryText, deliveryModeColour, orderReference, takenByText, doneCounter } =
  useStationOrderHeader(() => props.stationOrder)

const isGrouped = ref(false)
const cardItems = computed(() => itemsNotHandedOutIn(props.stationOrder))
const selectedHere = computed(() =>
  selectedOpenItemIds(props.stationOrder, props.selectedItemIds),
)
const cancelledSelectedHere = computed(() =>
  selectedItemIdsAwaitingAcknowledgement(props.stationOrder, props.selectedItemIds),
)
const groupedLines = computed(() => itemLines(cardItems.value))
const viewToggleLabel = computed(() =>
  t(isGrouped.value ? 'station.board.actions.listView' : 'station.board.actions.groupedView'),
)
const viewToggleIcon = computed(() =>
  isGrouped.value ? 'mdi-format-list-bulleted' : 'mdi-format-list-group',
)

function isSelected(orderItemId: string): boolean {
  return selectedHere.value.includes(orderItemId) || cancelledSelectedHere.value.includes(orderItemId)
}
</script>

<template>
  <BaseStationCard
    class="station-order mb-4 bg-surface"
    data-test="station-order"
    :data-test-id="stationOrder.stationOrderId"
    :delivery-mode-colour="deliveryModeColour"
  >
    <div class="station-order-head d-flex flex-wrap align-baseline ga-2">
      <span class="table-name app-section-title" data-test="table-name">
        {{ t('common.labels.table', { name: stationOrder.tableName }) }}
      </span>
      <span class="station-order-heading app-small-text text-medium-emphasis" data-test="station-order-heading">
        {{ orderReference }}
      </span>
      <span class="taken-by app-small-text text-medium-emphasis" data-test="taken-by">
        {{ takenByText }}
      </span>
      <span class="done-counter app-small-text ms-auto" data-test="done-counter">{{ doneCounter }}</span>
    </div>
    <div class="station-order-mode-row d-flex flex-wrap align-center ga-2 mb-1">
      <span class="delivery-mode app-text font-weight-medium" data-test="delivery-mode" :style="{ color: deliveryModeColour }">
        {{ deliveryText }}
      </span>
      <v-btn
        class="grouped-toggle ms-auto"
        data-test="grouped-toggle"
        variant="outlined"
        color="primary"
        :prepend-icon="viewToggleIcon"
        @click="isGrouped = !isGrouped"
      >
        {{ viewToggleLabel }}
      </v-btn>
    </div>
    <v-divider class="my-2" />
    <template v-if="!isGrouped">
      <v-btn
        v-for="item in cardItems"
        :key="item.orderItemId"
        class="station-item"
        data-test="station-item"
        :data-test-id="item.orderItemId"
        :class="{ selected: isSelected(item.orderItemId) }"
        variant="tonal"
        block
        @click="emit('toggleItem', item.orderItemId)"
      >
        <span class="item-text">
          <span class="item-name app-item-name" data-test="item-name" :class="{ 'cancelled-item': isCancelled(item.state) }">
            {{ item.itemName }}
          </span>
          <span
            v-if="item.note !== null"
            class="item-note app-small-text"
            data-test="item-note"
            :class="{ 'cancelled-item': isCancelled(item.state) }"
          >
            <v-icon class="item-note-icon" data-test="item-note-icon" icon="mdi-note-text-outline" size="small" />
            {{ t('common.labels.note', { note: item.note }) }}
          </span>
          <span v-if="isCancelled(item.state)" class="cancelled-at app-small-text" data-test="cancelled-at">
            {{ cancellationText(item, t, locale) }}
          </span>
        </span>
        <v-icon v-if="isSelected(item.orderItemId)" class="selected-tick ms-auto" data-test="selected-tick" icon="mdi-check" />
      </v-btn>
    </template>
    <div v-else class="grouped-items">
      <p
        v-for="line in groupedLines"
        :key="line.key"
        class="grouped-line app-item-name mb-0"
        data-test="grouped-line"
        :class="{ 'cancelled-item': line.isCancelled }"
      >
        {{ itemLineText(line, t) }}
      </p>
    </div>
    <div v-if="!isGrouped" class="card-actions d-flex mt-3" data-test="card-actions">
      <v-btn
        v-if="cancelledSelectedHere.length > 0"
        class="acknowledge"
        data-test="acknowledge"
        color="primary"
        :disabled="isWorking"
        @click="emit('acknowledge', cancelledSelectedHere)"
      >
        {{ t('station.board.actions.acknowledgeCancellation') }}
      </v-btn>
      <v-btn
        v-else
        class="fulfill"
        data-test="fulfill"
        color="primary"
        :disabled="selectedHere.length === 0 || isWorking"
        @click="emit('fulfill', stationOrder, selectedHere)"
      >
        {{ t('station.board.actions.prepare') }}
      </v-btn>
      <v-btn
        v-if="showHide"
        class="hide"
        data-test="hide"
        variant="outlined"
        :disabled="isWorking"
        @click="emit('hide', stationOrder.stationOrderId)"
      >
        {{ t('station.board.actions.hideHere') }}
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
