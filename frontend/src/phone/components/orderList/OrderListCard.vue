<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TableOrderRecordItemView, TableOrderRecordView } from '../../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { formatFestivalMoment } from '../../../shared/core/moments'
import { formatPrice } from '../../../shared/core/money'
import { isCancelled, isHandedOut } from '../../../shared/core/orderItemStates'
import {
  canBeSelected,
  selectableItemIdsInOrder,
  selectionStateOf,
  type SelectionPurpose,
} from '../../core/openItems'
import ItemHistoryDialog from './ItemHistoryDialog.vue'

const props = withDefaults(
  defineProps<{
    order: TableOrderRecordView
    language: AppLanguage
    purpose?: SelectionPurpose
    selectedItemIds?: string[]
  }>(),
  { purpose: undefined, selectedItemIds: () => [] },
)
const emit = defineEmits<{
  'toggle-item': [orderItemId: string]
  'set-whole-order': [order: TableOrderRecordView, isWanted: boolean]
}>()

const { t } = useI18n()

const itemWithHistoryShown = ref<TableOrderRecordItemView | null>(null)

const isSelecting = computed(() => props.purpose !== undefined)

const selectableItemIds = computed(() =>
  props.purpose === undefined ? [] : selectableItemIdsInOrder(props.order, props.purpose),
)

const orderSelection = computed(() => selectionStateOf(selectableItemIds.value, props.selectedItemIds))

const takenByText = computed(() =>
  t('common.labels.takenBy', {
    time: formatFestivalMoment(props.order.createdAtUtc, props.language),
    name: props.order.staffMemberName,
  }),
)

const doneCounterText = computed(() =>
  t('common.labels.doneCounter', {
    fulfilled: props.order.handedOutItemCount,
    total: props.order.notCancelledItemCount,
  }),
)

function isSelectable(item: TableOrderRecordItemView): boolean {
  return props.purpose !== undefined && canBeSelected(item, props.purpose)
}

function isMarkedHandedOut(item: TableOrderRecordItemView): boolean {
  return isCancelled(item.state) ? item.wasHandedOutWhenCancelled : isHandedOut(item.state)
}

function markIconFor(isDone: boolean): string {
  return isDone ? 'mdi-check' : 'mdi-close'
}

function markColourFor(isDone: boolean): string {
  return isDone ? 'success' : 'error'
}
</script>

<template>
  <v-card class="order-list-card mb-3" data-test="order-card" :data-test-id="order.globalOrderNumber">
    <v-card-text class="order-grid" :class="{ selecting: isSelecting }">
      <div class="grid-row" data-test="order-header">
        <div v-if="isSelecting" class="tick-cell">
          <v-checkbox-btn
            v-if="selectableItemIds.length > 0"
            data-test="whole-order"
            :model-value="orderSelection === 'all'"
            :indeterminate="orderSelection === 'some'"
            @update:model-value="emit('set-whole-order', order, orderSelection !== 'all')"
          />
        </div>
        <div class="order-head d-flex flex-wrap align-baseline ga-2">
          <span class="text-h6" data-test="order-number">
            {{ t('phone.openItems.labels.order', { order: order.globalOrderNumber }) }}
          </span>
          <span class="text-body-2 text-medium-emphasis" data-test="taken-by">{{ takenByText }}</span>
        </div>
      </div>
      <div class="grid-row" data-test="column-headings">
        <span class="done-counter text-body-2" data-test="done-counter">{{ doneCounterText }}</span>
        <span class="column-heading text-body-2" data-test="paid-heading">{{ t('phone.openItems.labels.paid') }}</span>
        <span class="column-heading text-body-2" data-test="handed-out-heading">
          {{ t('phone.orderOverview.labels.handedOut') }}
        </span>
      </div>
      <div
        v-for="item in order.items"
        :key="item.orderItemId"
        class="grid-row order-line"
        data-test="order-line"
        :data-test-id="item.orderItemId"
        @click="itemWithHistoryShown = item"
      >
        <div v-if="isSelecting" class="tick-cell" data-test="tick-cell" :data-test-id="item.orderItemId">
          <div v-if="isSelectable(item)" @click.stop>
            <v-checkbox-btn
              data-test="line-tick"
              :model-value="selectedItemIds.includes(item.orderItemId)"
              @update:model-value="emit('toggle-item', item.orderItemId)"
            />
          </div>
        </div>
        <div class="line-name-cell" :class="{ 'cancelled-item': isCancelled(item.state) }" data-test="line-name-cell">
          <div class="text-body-1" data-test="line-name">{{ item.itemName }}</div>
          <div v-if="item.note !== null" class="text-body-2 text-medium-emphasis" data-test="line-note">
            {{ t('common.labels.note', { note: item.note }) }}
          </div>
        </div>
        <span
          v-if="isSelecting"
          class="line-price text-body-1"
          :class="{ 'cancelled-item': isCancelled(item.state) }"
          data-test="line-price"
        >
          {{ formatPrice(item.unitPriceCents, language) }}
        </span>
        <v-icon
          class="line-mark"
          data-test="paid-mark"
          :data-test-id="item.orderItemId"
          :color="markColourFor(item.settledAtUtc !== null)"
          :icon="markIconFor(item.settledAtUtc !== null)"
        />
        <v-icon
          class="line-mark"
          data-test="handed-out-mark"
          :data-test-id="item.orderItemId"
          :color="markColourFor(isMarkedHandedOut(item))"
          :icon="markIconFor(isMarkedHandedOut(item))"
        />
      </div>
    </v-card-text>
    <ItemHistoryDialog
      v-if="itemWithHistoryShown !== null"
      :item="itemWithHistoryShown"
      :language="language"
      @close="itemWithHistoryShown = null"
    />
  </v-card>
</template>

<style scoped>
.order-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto auto;
  align-items: center;
  column-gap: 1rem;
  row-gap: 0.75rem;
}

.order-grid.selecting {
  grid-template-columns: 2.5rem minmax(0, 1fr) auto auto auto;
}

.order-head {
  grid-column: 1 / -1;
}

.order-grid.selecting .order-head {
  grid-column: 2 / -1;
}

.done-counter {
  grid-column: 1;
}

.order-grid.selecting .done-counter {
  grid-column: 1 / span 3;
}

.column-heading,
.line-mark {
  justify-self: center;
}

.grid-row {
  display: contents;
}

.order-line {
  cursor: pointer;
}

.tick-cell {
  display: flex;
  justify-content: center;
  margin-inline: -0.5rem;
}

.line-name-cell {
  min-width: 0;
  overflow-wrap: anywhere;
}

.line-price {
  justify-self: end;
  white-space: nowrap;
}
</style>
