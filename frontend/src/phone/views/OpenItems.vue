<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { isAPaymentMethodNeeded, TABLE_LOOKUP_DEBOUNCE_MS } from '../core/openItems'
import { formatPrice } from '../../shared/core/money'
import { useOpenItemsStore } from '../stores/openItems'
import { useSessionStore } from '../../shared/stores/session'
import { useDebounced } from '../../shared/composables/useDebounced'
import { useSettleDialog } from '../composables/useSettleDialog'
import OpenTablesList from '../components/openItems/OpenTablesList.vue'
import OpenTableLookup from '../components/openItems/OpenTableLookup.vue'
import AmountPaidDialog from '../components/openItems/AmountPaidDialog.vue'
import SettleNotice from '../components/openItems/SettleNotice.vue'
import TableField from '../components/review/TableField.vue'

const { t } = useI18n()
const openItems = useOpenItemsStore()
const session = useSessionStore()
const { amountPaidIsOpen, settleAtFullPrice, settleTheAmountPaid } = useSettleDialog()
const tableReportLookup = useDebounced((tableName: string) => {
  void openItems.loadTableReport(tableName)
}, TABLE_LOOKUP_DEBOUNCE_MS)

const openedTables = ref<string[]>([])
const typedTableName = ref('')
let stopListening: (() => void) | null = null

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

onMounted(async () => {
  typedTableName.value = openItems.lookupName ?? ''
  stopListening = openItems.listen()
  await openItems.load()
  await openItems.loadTableNames()
})

onUnmounted(() => {
  openItems.closeLookup()
  stopListening?.()
  stopListening = null
})

watch(typedTableName, (typed) => {
  tableReportLookup.cancel()
  const tableName = typed.trim()
  if (tableName.length === 0) {
    openItems.closeLookup()
    return
  }
  openItems.openLookup(tableName)
  tableReportLookup.callAfterTheDelay(tableName)
})
</script>

<template>
  <v-container class="open-items">
    <div class="head d-flex align-center ga-3 mb-2">
      <h1 class="text-h5 flex-grow-1">{{ t('phone.openItems.title') }}</h1>
      <v-btn class="reload" data-test="reload" variant="outlined" size="large" @click="openItems.reloadTheActiveView()">
        {{ t('phone.openItems.actions.reload') }}
      </v-btn>
    </div>
    <TableField
      v-model="typedTableName"
      :is-missing="false"
      :known-table-names="openItems.knownTableNames"
    />
    <v-alert v-if="theActiveViewFailed" class="load-failed mb-2" data-test="load-failed" type="warning" variant="tonal">
      {{ t('phone.openItems.errors.loadFailed') }}
    </v-alert>
    <SettleNotice
      v-if="openItems.notice !== null && !amountPaidIsOpen"
      class="mb-2"
      :notice="openItems.notice"
      closable
      @dismiss="openItems.dismissNotice"
    />
    <OpenTablesList
      v-if="!openItems.isLookingUp"
      v-model:opened-tables="openedTables"
      :tables="openItems.tables"
      :selected-item-ids="openItems.selectedItemIds"
      :language="session.language"
      :items-without-an-order-count="openItems.itemsWithoutAnOrderCount"
      :everything-is-settled="everythingIsSettled"
      @toggle-item="openItems.toggleItem"
      @set-whole-table="openItems.setWholeTable"
    />
    <OpenTableLookup
      v-else
      :report="openItems.lookupReport"
      :table="openItems.lookupTable"
      :selected-item-ids="openItems.selectedItemIds"
      :language="session.language"
      @toggle-item="openItems.toggleItem"
      @set-whole-table="openItems.setWholeTable"
      @set-whole-order="openItems.setWholeOrder"
    />
    <v-sheet v-if="somethingIsSelected" class="settle-footer pt-3 pb-4" data-test="settle-footer" color="background">
      <p class="selected-total text-h6 mb-2" data-test="selected-total">
        {{ t('phone.openItems.labels.selected', { amount: selectedTotal }) }}
      </p>
      <template v-if="isAPaymentMethodNeeded(openItems.selectedTotalCents)">
        <v-btn
          class="settle-in-cash"
          data-test="settle-in-cash"
          color="primary"
          block
          size="x-large"
          :disabled="openItems.isSettling"
          @click="settleAtFullPrice('cash')"
        >
          {{ t('phone.openItems.actions.settleInCash') }}
        </v-btn>
        <v-btn
          class="settle-by-card mt-2"
          data-test="settle-by-card"
          color="primary"
          block
          size="x-large"
          :disabled="openItems.isSettling"
          @click="settleAtFullPrice('card')"
        >
          {{ t('phone.openItems.actions.settleByCard') }}
        </v-btn>
      </template>
      <v-btn
        v-else
        class="settle-nothing-paid"
        data-test="settle-nothing-paid"
        color="primary"
        block
        size="x-large"
        :disabled="openItems.isSettling"
        @click="settleAtFullPrice('none')"
      >
        {{ t('phone.openItems.actions.settle') }}
      </v-btn>
      <v-btn
        class="settle-amount-paid mt-2"
        data-test="settle-amount-paid"
        color="primary"
        variant="outlined"
        block
        size="large"
        :disabled="openItems.isSettling"
        @click="amountPaidIsOpen = true"
      >
        {{ t('phone.openItems.actions.settleAmountPaid') }}
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
