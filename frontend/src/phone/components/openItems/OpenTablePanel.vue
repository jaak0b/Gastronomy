<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { OpenTableView } from '../../../shared/api/generatedSchemas'
import type { AppLanguage } from '../../../shared/core/deviceLanguage'
import { itemIdsAtTable, selectionStateOf } from '../../core/openItems'
import { formatPrice } from '../../../shared/core/money'
import OpenPositionRow from './OpenPositionRow.vue'

const props = defineProps<{
  table: OpenTableView
  selectedItemIds: string[]
  language: AppLanguage
  isHeldBackByAnotherTable: boolean
}>()
const emit = defineEmits<{
  'toggle-item': [orderItemId: string]
  'set-whole-table': [isWanted: boolean]
}>()

const { t } = useI18n()

const wholeTableSelection = computed(() =>
  selectionStateOf(itemIdsAtTable(props.table), props.selectedItemIds),
)

function priceTextFor(cents: number): string {
  return formatPrice(cents, props.language)
}

function isSelected(orderItemId: string): boolean {
  return props.selectedItemIds.includes(orderItemId)
}

function toggleItem(orderItemId: string): void {
  if (props.isHeldBackByAnotherTable) {
    return
  }
  emit('toggle-item', orderItemId)
}

function setWholeTable(): void {
  if (props.isHeldBackByAnotherTable) {
    return
  }
  emit('set-whole-table', wholeTableSelection.value !== 'all')
}
</script>

<template>
  <v-expansion-panel
    class="open-table"
    data-test="open-table"
    :data-test-id="table.tableName"
    :value="table.tableName"
    :disabled="isHeldBackByAnotherTable"
  >
    <v-expansion-panel-title data-test="open-table-title">
      <span class="table-name text-h6" data-test="table-name">{{ t('common.labels.table', { name: table.tableName }) }}</span>
      <v-spacer />
      <span class="open-amount text-body-1" data-test="open-amount">
        {{ t('phone.openItems.labels.tableOpen', { amount: priceTextFor(table.openAmountCents) }) }}
      </span>
    </v-expansion-panel-title>
    <v-expansion-panel-text>
      <template v-if="table.items.length > 0">
        <v-checkbox
          class="whole-table"
          data-test="whole-table"
          density="comfortable"
          hide-details
          :disabled="isHeldBackByAnotherTable"
          :label="t('phone.openItems.actions.wholeTable')"
          :model-value="wholeTableSelection === 'all'"
          :indeterminate="wholeTableSelection === 'some'"
          @update:model-value="setWholeTable"
        />
        <v-list class="open-lines" lines="three">
          <OpenPositionRow
            v-for="item in table.items"
            :key="item.orderItemId"
            :item-name="item.itemName"
            :note="item.note"
            :order-label="t('phone.openItems.labels.fromOrder', { number: item.globalOrderNumber })"
            :price-text="priceTextFor(item.unitPriceCents)"
            :is-selected="isSelected(item.orderItemId)"
            :is-disabled="isHeldBackByAnotherTable"
            :is-settled="false"
            production-state="unknown"
            @toggle="toggleItem(item.orderItemId)"
          />
        </v-list>
      </template>
    </v-expansion-panel-text>
  </v-expansion-panel>
</template>
