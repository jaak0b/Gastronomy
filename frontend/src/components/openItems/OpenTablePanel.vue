<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AppLanguage, OpenTable } from '../../core/apiTypes'
import { isTheWholeTableSelected } from '../../core/openItems'
import { formatPrice } from '../../core/totals'

const props = defineProps<{
  table: OpenTable
  selectedItemIds: string[]
  language: AppLanguage
}>()
const emit = defineEmits<{
  'toggle-item': [orderItemId: string]
  'set-whole-table': [isWanted: boolean]
}>()

const { t } = useI18n()

const wholeTableIsSelected = computed(() =>
  isTheWholeTableSelected(props.table, props.selectedItemIds),
)

function priceOf(cents: number): string {
  return formatPrice(cents, props.language)
}

function isSelected(orderItemId: string): boolean {
  return props.selectedItemIds.includes(orderItemId)
}
</script>

<template>
  <v-expansion-panel class="open-table" :value="table.tableName">
    <v-expansion-panel-title>
      <span class="table-name text-h6">{{ table.tableName }}</span>
      <v-spacer />
      <span class="open-amount text-body-1">
        {{ t('openItems.tableOpen', { amount: priceOf(table.openAmountCents) }) }}
      </span>
    </v-expansion-panel-title>
    <v-expansion-panel-text>
      <template v-if="table.items.length > 0">
        <v-checkbox
          class="whole-table"
          density="comfortable"
          hide-details
          :label="t('openItems.wholeTable')"
          :model-value="wholeTableIsSelected"
          @update:model-value="emit('set-whole-table', !wholeTableIsSelected)"
        />
        <v-list class="open-lines" lines="two">
          <v-list-item
            v-for="item in table.items"
            :key="item.orderItemId"
            class="open-line"
            @click="emit('toggle-item', item.orderItemId)"
          >
            <template #prepend>
              <v-checkbox-btn
                class="line-tick"
                readonly
                :model-value="isSelected(item.orderItemId)"
              />
            </template>
            <v-list-item-title class="line-name">{{ item.itemName }}</v-list-item-title>
            <v-list-item-subtitle class="line-origin">
              {{ t('openItems.fromOrder', { number: item.globalOrderNumber }) }}
            </v-list-item-subtitle>
            <v-list-item-subtitle v-if="item.note !== null" class="line-note">
              {{ t('openItems.itemNote', { note: item.note }) }}
            </v-list-item-subtitle>
            <template #append>
              <span class="line-price text-body-1">{{ priceOf(item.unitPriceCents) }}</span>
            </template>
          </v-list-item>
        </v-list>
      </template>
      <section v-if="table.givenAwayItems.length > 0" class="given-away mt-2">
        <v-divider class="mb-3" />
        <h2 class="given-away-heading text-subtitle-1 font-weight-medium">
          {{ t('openItems.givenAwayHeading', { amount: priceOf(table.givenAwayAmountCents) }) }}
        </h2>
        <p class="given-away-help text-body-2 text-medium-emphasis">
          {{ t('openItems.givenAwayHelp') }}
        </p>
        <v-list class="given-away-lines" lines="two">
          <v-list-item
            v-for="item in table.givenAwayItems"
            :key="item.orderItemId"
            class="given-away-line"
          >
            <v-list-item-title class="given-away-name">{{ item.itemName }}</v-list-item-title>
            <v-list-item-subtitle class="given-away-origin">
              {{ t('openItems.fromOrder', { number: item.globalOrderNumber }) }}
            </v-list-item-subtitle>
            <v-list-item-subtitle v-if="item.paymentNotice !== null" class="given-away-reason">
              {{ t('openItems.givenAwayReason', { reason: item.paymentNotice }) }}
            </v-list-item-subtitle>
            <template #append>
              <span class="given-away-price text-body-1">
                {{ priceOf(item.waivedAmountCents) }}
              </span>
            </template>
          </v-list-item>
        </v-list>
      </section>
    </v-expansion-panel-text>
  </v-expansion-panel>
</template>
