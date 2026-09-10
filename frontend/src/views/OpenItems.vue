<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { OpenTable } from '../core/apiTypes'
import { assertNever } from '../core/assertNever'
import { isHeldBackByAnotherTable, type SettleOutcome } from '../core/openItems'
import { formatPrice } from '../core/totals'
import { useOpenItemsStore } from '../stores/openItems'
import { useSessionStore } from '../stores/session'
import OpenTablePanel from '../components/openItems/OpenTablePanel.vue'
import AmountPaidDialog from '../components/openItems/AmountPaidDialog.vue'
import SettleNotice from '../components/openItems/SettleNotice.vue'

const { t } = useI18n()
const openItems = useOpenItemsStore()
const session = useSessionStore()

const amountPaidIsOpen = ref(false)
const openedTables = ref<string[]>([])
let stopListening: (() => void) | null = null

const selectedTotal = computed(() =>
  formatPrice(openItems.selectedTotalCents, session.language),
)
const somethingIsSelected = computed(() => openItems.selectedItemIds.length > 0)

const everythingIsSettled = computed(
  () => openItems.hasLoaded && !openItems.loadFailed && openItems.tables.length === 0,
)

onMounted(async () => {
  stopListening = openItems.listen()
  await openItems.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})

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

function isHeldBack(table: OpenTable): boolean {
  return isHeldBackByAnotherTable(openItems.tables, openItems.selectedItemIds, table.tableName)
}
</script>

<template>
  <v-container class="open-items">
    <div class="head d-flex align-center ga-3 mb-2">
      <h1 class="text-h5 flex-grow-1">{{ t('openItems.title') }}</h1>
      <v-btn class="reload" variant="outlined" size="large" @click="openItems.load">
        {{ t('openItems.reload') }}
      </v-btn>
    </div>
    <v-alert v-if="openItems.loadFailed" class="load-failed mb-2" type="warning" variant="tonal">
      {{ t('openItems.loadFailed') }}
    </v-alert>
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
    <SettleNotice
      v-if="openItems.notice !== null && !amountPaidIsOpen"
      class="mb-2"
      :notice="openItems.notice"
      closable
      @dismiss="openItems.dismissNotice"
    />
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
</style>
