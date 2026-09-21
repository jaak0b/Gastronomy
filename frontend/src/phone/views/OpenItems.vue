<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import type { OpenTable, TableOrderRecord } from '../../shared/api/apiTypes'
import { assertNever } from '../../shared/core/assertNever'
import { formatFestivalMoment } from '../../shared/core/festivalTimes'
import {
  isHeldBackByAnotherTable,
  isTheWholeTableSelected,
  positionStateOf,
  producedCountIn,
  productionStateOf,
  TABLE_LOOKUP_DEBOUNCE_MS,
  type SettleOutcome,
} from '../core/openItems'
import { formatPrice } from '../core/totals'
import { useOpenItemsStore } from '../stores/openItems'
import { useSessionStore } from '../../shared/stores/session'
import OpenTablePanel from '../components/openItems/OpenTablePanel.vue'
import OpenPositionRow from '../components/openItems/OpenPositionRow.vue'
import AmountPaidDialog from '../components/openItems/AmountPaidDialog.vue'
import SettleNotice from '../components/openItems/SettleNotice.vue'
import TableField from '../components/review/TableField.vue'

const { t } = useI18n()
const openItems = useOpenItemsStore()
const session = useSessionStore()

const amountPaidIsOpen = ref(false)
const openedTables = ref<string[]>([])
const typedTableName = ref('')
let stopListening: (() => void) | null = null
let lookupTimer: ReturnType<typeof setTimeout> | null = null

const selectedTotal = computed(() =>
  formatPrice(openItems.selectedTotalCents, session.language),
)
const somethingIsSelected = computed(() => openItems.selectedItemIds.length > 0)

const everythingIsSettled = computed(
  () => openItems.hasLoaded && !openItems.loadFailed && openItems.tables.length === 0,
)

const theActiveViewFailed = computed(() =>
  openItems.isLookingUp ? openItems.lookupFailed : openItems.loadFailed,
)

const ordersInTheLookup = computed(() => openItems.lookupReport?.orders ?? [])

const lookupFoundNothing = computed(
  () => openItems.lookupReport !== null && ordersInTheLookup.value.length === 0,
)

const wholeTableIsSelected = computed(() =>
  openItems.lookupTable === null
    ? false
    : isTheWholeTableSelected(openItems.lookupTable, openItems.selectedItemIds),
)

onMounted(async () => {
  stopListening = openItems.listen()
  await openItems.load()
  await openItems.loadTableNames()
})

onUnmounted(() => {
  stopTheLookupTimer()
  openItems.closeLookup()
  stopListening?.()
  stopListening = null
})

watch(typedTableName, (typed) => {
  stopTheLookupTimer()
  const tableName = typed.trim()
  if (tableName.length === 0) {
    openItems.closeLookup()
    return
  }
  openItems.openLookup(tableName)
  lookupTimer = setTimeout(() => {
    lookupTimer = null
    void openItems.loadTableReport(tableName)
  }, TABLE_LOOKUP_DEBOUNCE_MS)
})

function stopTheLookupTimer(): void {
  if (lookupTimer !== null) {
    clearTimeout(lookupTimer)
    lookupTimer = null
  }
}

function reloadTheActiveView(): void {
  if (openItems.isLookingUp) {
    void openItems.refreshLookup()
    return
  }
  void openItems.load()
}

async function settle(): Promise<void> {
  await openItems.settle(openItems.selectedTotalCents, null)
}

async function settleTheAmountPaid(
  amountPaidCents: number,
  paymentNotice: string | null,
): Promise<void> {
  closeTheAmountAskedForUnlessTheLaptopRefused(
    await openItems.settle(amountPaidCents, paymentNotice),
  )
}

function closeTheAmountAskedForUnlessTheLaptopRefused(outcome: SettleOutcome): void {
  switch (outcome) {
    case 'accepted':
    case 'answerNeverCame':
      amountPaidIsOpen.value = false
      return
    case 'refused':
      return
    default:
      return assertNever(outcome)
  }
}

function setWholeTable(table: OpenTable, isWanted: boolean): void {
  openItems.setWholeTable(table, isWanted)
}

function setTheLookupWholeTable(isWanted: boolean | null): void {
  const table = openItems.lookupTable
  if (table !== null) {
    openItems.setWholeTable(table, isWanted === true)
  }
}

function isHeldBack(table: OpenTable): boolean {
  return isHeldBackByAnotherTable(openItems.tables, openItems.selectedItemIds, table.tableName)
}

function priceTextFor(cents: number): string {
  return formatPrice(cents, session.language)
}

function stateClassFor(order: TableOrderRecord): string {
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

function takenByTextFor(order: TableOrderRecord): string {
  return t('station.takenBy', {
    time: formatFestivalMoment(order.createdAtUtc, session.language),
    name: order.staffMemberName,
  })
}

function doneCounterTextFor(order: TableOrderRecord): string {
  return t('station.doneCounter', {
    fulfilled: producedCountIn(order),
    total: order.items.length,
  })
}
</script>

<template>
  <v-container class="open-items">
    <div class="head d-flex align-center ga-3 mb-2">
      <h1 class="text-h5 flex-grow-1">{{ t('openItems.title') }}</h1>
      <v-btn class="reload" variant="outlined" size="large" @click="reloadTheActiveView">
        {{ t('openItems.reload') }}
      </v-btn>
    </div>
    <TableField
      v-model="typedTableName"
      :is-missing="false"
      :known-table-names="openItems.knownTableNames"
    />
    <v-alert v-if="theActiveViewFailed" class="load-failed mb-2" type="warning" variant="tonal">
      {{ t('openItems.loadFailed') }}
    </v-alert>
    <SettleNotice
      v-if="openItems.notice !== null && !amountPaidIsOpen"
      class="mb-2"
      :notice="openItems.notice"
      closable
      @dismiss="openItems.dismissNotice"
    />
    <template v-if="!openItems.isLookingUp">
      <v-alert
        v-if="openItems.itemsWithoutAnOrderCount > 0"
        class="list-incomplete mb-2"
        type="warning"
        variant="tonal"
      >
        {{
          t(
            'openItems.listIncomplete',
            { count: openItems.itemsWithoutAnOrderCount },
            openItems.itemsWithoutAnOrderCount,
          )
        }}
      </v-alert>
      <v-alert v-if="everythingIsSettled" class="empty" type="info" variant="tonal">
        {{ t('openItems.empty') }}
      </v-alert>
      <v-expansion-panels v-model="openedTables" class="tables" multiple>
        <OpenTablePanel
          v-for="table in openItems.tables"
          :key="table.tableName"
          :table="table"
          :selected-item-ids="openItems.selectedItemIds"
          :language="session.language"
          :is-held-back-by-another-table="isHeldBack(table)"
          @toggle-item="openItems.toggleItem"
          @set-whole-table="setWholeTable(table, $event)"
        />
      </v-expansion-panels>
    </template>
    <template v-else>
      <v-checkbox
        v-if="openItems.lookupTable !== null && openItems.lookupTable.items.length > 0"
        class="whole-table"
        density="comfortable"
        hide-details
        :label="t('openItems.wholeTable')"
        :model-value="wholeTableIsSelected"
        @update:model-value="setTheLookupWholeTable"
      />
      <v-card
        v-for="order in ordersInTheLookup"
        :key="order.orderId"
        class="lookup-card mb-3"
        :class="stateClassFor(order)"
      >
        <v-card-text class="lookup-body">
          <div class="lookup-card-head d-flex flex-wrap align-baseline ga-2 mb-2">
            <span class="order-number text-h6">
              {{ t('openItems.order', { order: order.globalOrderNumber }) }}
            </span>
            <span class="taken-by text-body-2 text-medium-emphasis">
              {{ takenByTextFor(order) }}
            </span>
            <span class="done-counter text-body-2 ms-auto">{{ doneCounterTextFor(order) }}</span>
          </div>
          <v-list class="lookup-lines" lines="three">
            <OpenPositionRow
              v-for="item in order.items"
              :key="item.orderItemId"
              :item-name="item.itemName"
              :note="item.note"
              :order-label="null"
              :price-text="priceTextFor(item.unitPriceCents)"
              :is-selected="openItems.selectedItemIds.includes(item.orderItemId)"
              :is-disabled="item.settledAtUtc !== null"
              :is-settled="item.settledAtUtc !== null"
              :production-state="positionStateOf(item)"
              @toggle="openItems.toggleItem(item.orderItemId)"
            />
          </v-list>
        </v-card-text>
      </v-card>
      <v-alert v-if="lookupFoundNothing" class="lookup-empty" type="info" variant="tonal">
        {{ t('openItems.noOrdersForTable') }}
      </v-alert>
    </template>
    <v-sheet v-if="somethingIsSelected" class="settle-footer pt-3 pb-4" color="background">
      <p class="selected-total text-h6 mb-2">
        {{ t('openItems.selected', { amount: selectedTotal }) }}
      </p>
      <v-btn
        class="settle"
        color="primary"
        block
        size="x-large"
        :disabled="openItems.isSettling"
        @click="settle"
      >
        {{ t('openItems.settle') }}
      </v-btn>
      <v-btn
        class="settle-amount-paid mt-2"
        color="primary"
        variant="outlined"
        block
        size="large"
        :disabled="openItems.isSettling"
        @click="amountPaidIsOpen = true"
      >
        {{ t('openItems.settleAmountPaid') }}
      </v-btn>
    </v-sheet>
    <AmountPaidDialog
      v-if="amountPaidIsOpen"
      :is-settling="openItems.isSettling"
      :notice="openItems.notice"
      :selected-total-cents="openItems.selectedTotalCents"
      :language="session.language"
      @confirm="settleTheAmountPaid"
      @cancel="amountPaidIsOpen = false"
    />
  </v-container>
</template>

<style scoped>
.open-items {
  padding-bottom: 96px;
}

.settle-footer {
  position: sticky;
  bottom: 0;
  z-index: 2;
  border-top: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}

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
  padding: 0.75rem 1rem 0.5rem;
}

.lookup-lines {
  background: transparent;
  padding: 0;
}
</style>
