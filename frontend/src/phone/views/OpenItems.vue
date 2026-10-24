<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { formatPrice } from '../../shared/core/money'
import { navigate } from '../../shared/router/router'
import { useOpenItemsStore } from '../stores/openItems'
import { useSessionStore } from '../../shared/stores/session'
import { useSettleDialog } from '../composables/useSettleDialog'
import TableOrders from '../components/openItems/TableOrders.vue'
import AmountPaidDialog from '../components/openItems/AmountPaidDialog.vue'
import SettleNotice from '../components/openItems/SettleNotice.vue'

const { t } = useI18n()
const openItems = useOpenItemsStore()
const session = useSessionStore()
const { amountPaidIsOpen, settleTheAmountPaid } = useSettleDialog()
let stopListening: (() => void) | null = null

const selectedTotal = computed(() =>
  formatPrice(openItems.selectedTotalCents, session.language),
)
const somethingIsSelected = computed(() => openItems.selectedItemIds.length > 0)

onMounted(async () => {
  if (openItems.tableId === null) {
    navigate('/')
    return
  }
  stopListening = openItems.listen()
  await openItems.load()
})

onUnmounted(() => {
  openItems.closeTable()
  stopListening?.()
  stopListening = null
})
</script>

<template>
  <v-container class="open-items">
    <div class="head d-flex align-center ga-3 mb-2">
      <h1 class="text-h5 flex-grow-1" data-test="settle-title">{{ t('phone.openItems.title') }}</h1>
      <v-btn class="reload" data-test="reload" variant="outlined" size="large" @click="openItems.load()">
        {{ t('phone.openItems.actions.reload') }}
      </v-btn>
    </div>
    <v-alert
      v-if="openItems.loadFailureKey !== null"
      class="load-failed mb-2"
      data-test="load-failed"
      type="warning"
      variant="tonal"
    >
      {{ t(openItems.loadFailureKey) }}
    </v-alert>
    <SettleNotice
      v-if="openItems.notice !== null && !amountPaidIsOpen"
      class="mb-2"
      :notice="openItems.notice"
      closable
      @dismiss="openItems.dismissNotice"
    />
    <TableOrders
      :report="openItems.report"
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
      <v-btn
        class="settle"
        data-test="settle"
        color="primary"
        block
        size="x-large"
        @click="amountPaidIsOpen = true"
      >
        {{ t('phone.openItems.actions.settle') }}
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
