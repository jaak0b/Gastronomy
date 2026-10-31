<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { TableOrderRecordItemView, TableOrderRecordView } from '../../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { formatFestivalMoment } from '../../../shared/core/moments'
import { isCancelled, isHandedOut } from '../../../shared/core/orderItemStates'
import {
  canBeSelected,
  selectableItemIdsInOrder,
  selectionStateOf,
  type SelectionPurpose,
} from '../../core/openItems'
import BaseIconAction from '../../../shared/components/BaseIconAction.vue'
import ItemHistoryDialog from './ItemHistoryDialog.vue'
import { PHONE_ORDER_GRID_DEFAULTS } from '../../styles/phoneSizes'

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

const itemWithDetailsShown = ref<TableOrderRecordItemView | null>(null)

const hasTickColumn = computed(() => selectableItemIds.value.length > 0)

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

function isItemSelected(item: TableOrderRecordItemView): boolean {
  return isSelectable(item) && props.selectedItemIds.includes(item.orderItemId)
}

function toggleWhenSelectable(item: TableOrderRecordItemView): void {
  if (isSelectable(item)) {
    emit('toggle-item', item.orderItemId)
  }
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
    <v-defaults-provider :defaults="PHONE_ORDER_GRID_DEFAULTS">
      <v-card-text class="order-grid" :class="{ selecting: hasTickColumn }">
        <div class="order-header" data-test="order-header">
          <div v-if="hasTickColumn" class="tick-cell">
            <v-checkbox-btn
              v-if="selectableItemIds.length > 0"
              data-test="whole-order"
              :model-value="orderSelection === 'all'"
              :indeterminate="orderSelection === 'some'"
              @update:model-value="emit('set-whole-order', order, orderSelection !== 'all')"
            />
          </div>
          <div class="order-head d-flex flex-wrap align-baseline ga-2">
            <span class="app-section-title" data-test="order-number">
              {{ t('phone.openItems.labels.order', { order: order.globalOrderNumber }) }}
            </span>
            <span class="app-small-text text-medium-emphasis" data-test="taken-by">{{ takenByText }}</span>
          </div>
        </div>
        <div class="column-headings" data-test="column-headings">
          <span class="done-counter app-small-text text-medium-emphasis" data-test="done-counter">{{ doneCounterText }}</span>
          <span class="column-heading app-small-text" data-test="paid-heading">{{ t('phone.openItems.labels.paid') }}</span>
          <span class="column-heading app-small-text" data-test="handed-out-heading">
            {{ t('phone.orderOverview.labels.handedOut') }}
          </span>
          <span />
        </div>
        <div
          v-for="item in order.items"
          :key="item.orderItemId"
          class="order-line"
          :class="{ selectable: isSelectable(item), selected: isItemSelected(item) }"
          data-test="order-line"
          :data-test-id="item.orderItemId"
          :data-selected="purpose === undefined ? undefined : String(isItemSelected(item))"
          @click="toggleWhenSelectable(item)"
        >
          <div class="line-name-cell" :class="{ 'cancelled-item': isCancelled(item.state) }" data-test="line-name-cell">
            <div class="app-item-name" data-test="line-name">{{ item.itemName }}</div>
            <div v-if="item.note !== null" class="app-small-text text-medium-emphasis" data-test="line-note">
              {{ t('common.labels.note', { note: item.note }) }}
            </div>
          </div>
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
          <BaseIconAction
            data-test="item-details"
            :data-test-id="item.orderItemId"
            icon="mdi-information-outline"
            :label="t('phone.itemHistory.actions.showDetails')"
            @click.stop="itemWithDetailsShown = item"
          />
        </div>
      </v-card-text>
    </v-defaults-provider>
    <ItemHistoryDialog
      v-if="itemWithDetailsShown !== null"
      :item="itemWithDetailsShown"
      :language="language"
      @close="itemWithDetailsShown = null"
    />
  </v-card>
</template>

<style scoped>
.order-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto auto auto;
  align-items: center;
  column-gap: 0.5rem;
  row-gap: 0.75rem;
}

.order-grid.selecting {
  grid-template-columns: 2rem minmax(0, 1fr) auto auto auto;
}

.order-header,
.column-headings,
.order-line {
  grid-column: 1 / -1;
  display: grid;
  grid-template-columns: subgrid;
  align-items: center;
}

.order-line {
  padding-inline: 0.5rem;
  padding-block: 0.25rem;
}

.order-head {
  grid-column: 1 / -1;
}

.order-grid.selecting .order-head {
  grid-column: 2 / -1;
}

.done-counter {
  grid-column: 1;
  padding-inline-start: 0.5rem;
}

.order-grid.selecting .done-counter {
  grid-column: 1 / span 2;
}

.line-name-cell {
  grid-column: 1;
  min-width: 0;
  overflow-wrap: anywhere;
}

.order-grid.selecting .line-name-cell {
  grid-column: 1 / span 2;
}

.column-heading,
.line-mark {
  justify-self: center;
}

.order-line.selectable {
  cursor: pointer;
}

.order-line.selected {
  background-color: rgba(var(--v-theme-primary), 0.1);
  outline: 2px solid rgb(var(--v-theme-primary));
  outline-offset: -2px;
}

.tick-cell {
  display: flex;
  justify-content: center;
}
</style>
