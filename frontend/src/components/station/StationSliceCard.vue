<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { DeliveryMode, StationSlice } from '../../core/apiTypes'
import { assertNever } from '../../core/assertNever'
import { deliveryModeKey, openItemsOf, selectedOpenItemIds } from '../../core/stationBoard'

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

const openItems = computed(() => openItemsOf(props.slice))
const selectedHere = computed(() => selectedOpenItemIds(props.slice, props.selectedItemIds))
const deliveryText = computed(() => t(deliveryModeKey(props.slice.deliveryMode)))
const modeColour = computed(() => modeColourOf(props.slice.deliveryMode))
const modeClass = computed(() => modeClassOf(props.slice.deliveryMode))
const doneCounter = computed(() =>
  t('station.doneCounter', {
    fulfilled: props.slice.fulfilledItemCount,
    total: props.slice.itemCount,
  }),
)

function modeColourOf(deliveryMode: DeliveryMode): string {
  switch (deliveryMode) {
    case 'together':
      return 'primary'
    case 'asItComes':
      return 'warning'
    default:
      return assertNever(deliveryMode)
  }
}

function modeClassOf(deliveryMode: DeliveryMode): string {
  switch (deliveryMode) {
    case 'together':
      return 'mode-together'
    case 'asItComes':
      return 'mode-as-it-comes'
    default:
      return assertNever(deliveryMode)
  }
}

function isSelected(orderItemId: string): boolean {
  return selectedHere.value.includes(orderItemId)
}
</script>

<template>
  <v-card class="station-slice mb-4" variant="outlined">
    <v-card-title class="slice-heading text-subtitle-1">
      {{
        t('station.order', {
          order: slice.globalOrderNumber,
          sequence: slice.stationOrderNumber,
        })
      }}
    </v-card-title>
    <v-card-subtitle class="table-name text-h4">{{ slice.tableName }}</v-card-subtitle>
    <v-card-text>
      <v-chip
        class="delivery-mode"
        :class="modeClass"
        :color="modeColour"
        variant="flat"
        size="large"
      >
        {{ deliveryText }}
      </v-chip>
      <p class="done-counter text-body-1 mt-2 mb-0">{{ doneCounter }}</p>
      <p v-if="slice.note !== null" class="slice-note text-body-1 mt-1">
        {{ t('station.orderNote', { note: slice.note }) }}
      </p>
      <v-divider class="my-2" />
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
        <span class="item-name">{{ item.itemName }}</span>
        <span v-if="item.note !== null" class="item-note text-body-2">
          {{ t('station.note', { note: item.note }) }}
        </span>
      </v-btn>
    </v-card-text>
    <v-card-actions class="card-actions">
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
.station-item {
  justify-content: flex-start;
  height: auto;
  min-height: 3.5rem;
  padding-block: 0.5rem;
  text-transform: none;
  letter-spacing: normal;
}

.station-item + .station-item {
  margin-top: 0.5rem;
}

.station-item.selected {
  outline: 3px solid rgb(var(--v-theme-primary));
  outline-offset: -3px;
}

.station-item .item-note {
  margin-inline-start: 0.75rem;
}

.card-actions {
  gap: 0.75rem;
}

.card-actions .v-btn {
  flex: 1 1 auto;
  min-width: 0;
}
</style>
