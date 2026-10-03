<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import {
  OpenTableView,
  TableOrderRecordView,
  TableOrderReportView,
} from '../../../shared/api/generatedSchemas'
import { assertNever } from '../../../shared/core/assertNever'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { formatFestivalMoment } from '../../../shared/core/festivalTimes'
import {
  itemIdsAtTable,
  positionStateOf,
  producedCountIn,
  productionStateOf,
  selectionStateOf,
  unsettledItemIdsInOrder,
  type ShareState,
} from '../../core/openItems'
import { formatPrice } from '../../core/totals'
import OpenPositionRow from './OpenPositionRow.vue'

const props = defineProps<{
  report: TableOrderReportView | null
  table: OpenTableView | null
  selectedItemIds: string[]
  language: AppLanguage
}>()
const emit = defineEmits<{
  'toggle-item': [orderItemId: string]
  'set-whole-table': [table: OpenTableView, isWanted: boolean]
  'set-whole-order': [tableName: string, order: TableOrderRecordView, isWanted: boolean]
}>()

const { t } = useI18n()

const orders = computed(() => props.report?.orders ?? [])

const foundNothing = computed(() => props.report !== null && orders.value.length === 0)

const wholeTableSelection = computed<ShareState>(() =>
  props.table === null
    ? 'none'
    : selectionStateOf(itemIdsAtTable(props.table), props.selectedItemIds),
)

function setTheWholeTable(): void {
  if (props.table !== null) {
    emit('set-whole-table', props.table, wholeTableSelection.value !== 'all')
  }
}

function orderSelectionOf(order: TableOrderRecordView): ShareState {
  return selectionStateOf(unsettledItemIdsInOrder(order), props.selectedItemIds)
}

function setWholeOrder(order: TableOrderRecordView): void {
  if (props.report === null) {
    return
  }
  emit('set-whole-order', props.report.tableName, order, orderSelectionOf(order) !== 'all')
}

function priceTextFor(cents: number): string {
  return formatPrice(cents, props.language)
}

function stateClassFor(order: TableOrderRecordView): string {
  const state = productionStateOf(order)
  switch (state) {
    case 'none':
      return 'state-none'
    case 'some':
      return 'state-some'
    case 'all':
      return 'state-all'
    default:
      return assertNever(state)
  }
}

function takenByTextFor(order: TableOrderRecordView): string {
  return t('common.labels.takenBy', {
    time: formatFestivalMoment(order.createdAtUtc, props.language),
    name: order.staffMemberName,
  })
}

function doneCounterTextFor(order: TableOrderRecordView): string {
  return t('common.labels.doneCounter', {
    fulfilled: producedCountIn(order),
    total: order.items.length,
  })
}
</script>

<template>
  <v-checkbox
    v-if="table !== null && table.items.length > 0"
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
    class="lookup-card mb-3"
    :class="stateClassFor(order)"
    data-test="lookup-card"
    :data-test-id="order.globalOrderNumber"
    :data-state="productionStateOf(order)"
  >
    <v-card-text class="lookup-body">
      <div class="lookup-card-head d-flex flex-wrap align-baseline ga-2">
        <span class="text-h6" data-test="order-number">
          {{ t('phone.openItems.labels.order', { order: order.globalOrderNumber }) }}
        </span>
        <span class="text-body-2 text-medium-emphasis" data-test="taken-by">
          {{ takenByTextFor(order) }}
        </span>
      </div>
      <div class="lookup-card-tally d-flex align-center ga-2">
        <v-checkbox-btn
          v-if="unsettledItemIdsInOrder(order).length > 0"
          class="flex-grow-0"
          data-test="whole-order"
          :model-value="orderSelectionOf(order) === 'all'"
          :indeterminate="orderSelectionOf(order) === 'some'"
          @update:model-value="setWholeOrder(order)"
        />
        <span class="text-body-2 ms-auto" data-test="done-counter">
          {{ doneCounterTextFor(order) }}
        </span>
      </div>
      <v-list class="lookup-lines" lines="three">
        <OpenPositionRow
          v-for="item in order.items"
          :key="item.orderItemId"
          :item-name="item.itemName"
          :note="item.note"
          :order-label="null"
          :price-text="priceTextFor(item.unitPriceCents)"
          :is-selected="selectedItemIds.includes(item.orderItemId)"
          :is-disabled="item.settledAtUtc !== null"
          :is-settled="item.settledAtUtc !== null"
          :production-state="positionStateOf(item)"
          @toggle="emit('toggle-item', item.orderItemId)"
        />
      </v-list>
    </v-card-text>
  </v-card>
  <v-alert v-if="foundNothing" data-test="lookup-empty" type="info" variant="tonal">
    {{ t('phone.openItems.messages.noOrdersForTable') }}
  </v-alert>
</template>

<style scoped>
.lookup-card {
  background: rgb(var(--v-theme-surface));
}

.lookup-card.state-none {
  background: color-mix(in srgb, rgb(var(--v-theme-error)) 30%, rgb(var(--v-theme-surface)));
  border-inline-start: 0.375rem solid rgb(var(--v-theme-error));
}

.lookup-card.state-some {
  background: color-mix(in srgb, rgb(var(--v-theme-warning)) 30%, rgb(var(--v-theme-surface)));
  border-inline-start: 0.375rem solid rgb(var(--v-theme-warning));
}

.lookup-card.state-all {
  background: color-mix(in srgb, rgb(var(--v-theme-success)) 30%, rgb(var(--v-theme-surface)));
  border-inline-start: 0.375rem solid rgb(var(--v-theme-success));
}

.lookup-body {
  padding: 0;
}

.lookup-card-head {
  padding: 0.75rem 1rem 0;
}

.lookup-card-tally {
  min-height: 3rem;
  padding: 0 1rem;
}

.lookup-lines {
  background: transparent;
  padding: 0;
}
</style>
