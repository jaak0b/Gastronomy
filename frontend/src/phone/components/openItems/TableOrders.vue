<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type {
  ShareState,
  TableOrderRecordView,
  TableOrderReportView,
} from '../../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { selectableItemIdsIn, selectionStateOf, type SelectionPurpose } from '../../core/openItems'
import OrderListCard from '../orderList/OrderListCard.vue'

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
  <OrderListCard
    v-for="order in orders"
    :key="order.orderId"
    :order="order"
    :language="language"
    :purpose="purpose"
    :selected-item-ids="selectedItemIds"
    @toggle-item="emit('toggle-item', $event)"
    @set-whole-order="(order, isWanted) => emit('set-whole-order', order, isWanted)"
  />
  <v-alert v-if="foundNothing" data-test="no-orders" type="info" variant="tonal">
    {{ emptyText }}
  </v-alert>
</template>
