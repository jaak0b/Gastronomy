<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  ShareState,
  TableOrderRecordView,
  TableOrderReportView,
} from '../../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { formatFestivalMoment } from '../../../shared/core/moments'
import {
  canBeSelected,
  isShownAsPaid,
  selectableItemIdsIn,
  selectableItemIdsInOrder,
  selectionStateOf,
  type SelectionPurpose,
} from '../../core/openItems'
import { formatPrice } from '../../../shared/core/money'
import { isCancelled } from '../../../shared/core/orderItemStates'
import OpenPositionRow from './OpenPositionRow.vue'

const props = defineProps<{
  report: TableOrderReportView | null
  selectedItemIds: string[]
  purpose: SelectionPurpose
  emptyText: string
  language: AppLanguage
}>()
const emit = defineEmits<{
  'toggle-item': [orderItemId: string]
  'set-whole-table': [isWanted: boolean]
  'set-whole-order': [order: TableOrderRecordView, isWanted: boolean]
}>()

const { t } = useI18n()

const orders = computed(() => props.report?.orders ?? [])

const foundNothing = computed(() => props.report !== null && orders.value.length === 0)

const selectableItemIds = computed(() =>
  props.report === null ? [] : selectableItemIdsIn(props.report, props.purpose),
)

const wholeTableSelection = computed<ShareState>(() =>
  selectionStateOf(selectableItemIds.value, props.selectedItemIds),
)

function setTheWholeTable(): void {
  emit('set-whole-table', wholeTableSelection.value !== 'all')
}

function orderSelectionOf(order: TableOrderRecordView): ShareState {
  return selectionStateOf(selectableItemIdsInOrder(order, props.purpose), props.selectedItemIds)
}

function canSelectAnythingIn(order: TableOrderRecordView): boolean {
  return selectableItemIdsInOrder(order, props.purpose).length > 0
}

function setWholeOrder(order: TableOrderRecordView): void {
  emit('set-whole-order', order, orderSelectionOf(order) !== 'all')
}

function priceTextFor(cents: number): string {
  return formatPrice(cents, props.language)
}

function takenByTextFor(order: TableOrderRecordView): string {
  return t('common.labels.takenBy', {
    time: formatFestivalMoment(order.createdAtUtc, props.language),
    name: order.staffMemberName,
  })
}
</script>

<template>
  <v-checkbox
    v-if="selectableItemIds.length > 0"
    data-test="whole-table"
    density="comfortable"
    hide-details
    :label="t('phone.openItems.actions.wholeTable')"
    :model-value="wholeTableSelection === 'all'"
    :indeterminate="wholeTableSelection === 'some'"
    @update:model-value="setTheWholeTable"
  />
  <v-card
    v-for="order in orders"
    :key="order.orderId"
    class="order-card mb-3"
    data-test="order-card"
    :data-test-id="order.globalOrderNumber"
  >
    <v-card-text class="order-body">
      <div class="order-card-head d-flex flex-wrap align-baseline ga-2">
        <span class="text-h6" data-test="order-number">
          {{ t('phone.openItems.labels.order', { order: order.globalOrderNumber }) }}
        </span>
        <span class="text-body-2 text-medium-emphasis" data-test="taken-by">
          {{ takenByTextFor(order) }}
        </span>
      </div>
      <div v-if="canSelectAnythingIn(order)" class="order-card-tally d-flex align-center ga-2">
        <v-checkbox-btn
          class="flex-grow-0"
          data-test="whole-order"
          :model-value="orderSelectionOf(order) === 'all'"
          :indeterminate="orderSelectionOf(order) === 'some'"
          @update:model-value="setWholeOrder(order)"
        />
      </div>
      <v-list class="order-lines" lines="three">
        <OpenPositionRow
          v-for="item in order.items"
          :key="item.orderItemId"
          :item-name="item.itemName"
          :note="item.note"
          :price-text="priceTextFor(item.unitPriceCents)"
          :is-selected="selectedItemIds.includes(item.orderItemId)"
          :is-selectable="canBeSelected(item, purpose)"
          :is-cancelled="isCancelled(item.state)"
          :is-shown-as-paid="isShownAsPaid(item, purpose)"
          @toggle="emit('toggle-item', item.orderItemId)"
        />
      </v-list>
    </v-card-text>
  </v-card>
  <v-alert v-if="foundNothing" data-test="no-orders" type="info" variant="tonal">
    {{ emptyText }}
  </v-alert>
</template>

<style scoped>
.order-body {
  padding: 0;
}

.order-card-head {
  padding: 0.75rem 1rem 0;
}

.order-card-tally {
  min-height: 3rem;
  padding: 0 1rem;
}

.order-lines {
  background: transparent;
  padding: 0;
}
</style>
