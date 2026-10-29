<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../shared/core/assertNever'
import { formatPrice } from '../../shared/core/money'
import { navigate } from '../../shared/router/router'
import { defaultFilterFor } from '../core/openItems'
import { useOpenItemsStore } from '../stores/openItems'
import { useSessionStore } from '../../shared/stores/session'
import { useSettleDialog } from '../composables/useSettleDialog'
import OrderListFilter from '../components/orderList/OrderListFilter.vue'
import OrderListTitle from '../components/orderList/OrderListTitle.vue'
import TableOrders from '../components/openItems/TableOrders.vue'
import AmountPaidDialog from '../components/openItems/AmountPaidDialog.vue'
import CancelItemsDialog from '../components/openItems/CancelItemsDialog.vue'
import SettleNotice from '../components/openItems/SettleNotice.vue'

const { t } = useI18n()
const openItems = useOpenItemsStore()
const session = useSessionStore()
const { amountPaidIsOpen, settleTheAmountPaid } = useSettleDialog()
const cancelConfirmationIsOpen = ref(false)
let stopListening: (() => void) | null = null

const somethingIsSelected = computed(() => openItems.selectedItemIds.length > 0)
const aPageIsOpen = computed(() => amountPaidIsOpen.value || cancelConfirmationIsOpen.value)

const title = computed(() => {
  switch (openItems.purpose) {
    case 'settling':
      return t('phone.openItems.title')
    case 'cancelling':
      return t('phone.openItems.cancellingTitle')
    default:
      return assertNever(openItems.purpose)
  }
})

const filterIsTheDefault = computed(() => {
  const standard = defaultFilterFor(openItems.purpose)
  const picked = openItems.filter
  return (
    picked.orderStatus === standard.orderStatus &&
    picked.paymentStatus === standard.paymentStatus &&
    picked.includeCancelled === standard.includeCancelled
  )
})

const emptyText = computed(() => {
  if (!filterIsTheDefault.value) {
    return t('phone.orderOverview.messages.noOrderMatches')
  }
  switch (openItems.purpose) {
    case 'settling':
      return t('phone.openItems.messages.nothingToSettle')
    case 'cancelling':
      return t('phone.openItems.messages.nothingToCancel')
    default:
      return assertNever(openItems.purpose)
  }
})

const selectedText = computed(() => {
  switch (openItems.purpose) {
    case 'settling':
      return t('phone.openItems.labels.selected', {
        amount: formatPrice(openItems.selectedTotalCents, session.language),
      })
    case 'cancelling':
      return t(
        'phone.openItems.labels.selectedCount',
        { count: openItems.selectedItemIds.length },
        openItems.selectedItemIds.length,
      )
    default:
      return assertNever(openItems.purpose)
  }
})

const footerAction = computed(() => {
  switch (openItems.purpose) {
    case 'settling':
      return { test: 'settle', text: t('phone.openItems.actions.settleSelected') }
    case 'cancelling':
      return { test: 'cancel-items', text: t('phone.openItems.actions.cancelSelected') }
    default:
      return assertNever(openItems.purpose)
  }
})

function openThePageForTheSelection(): void {
  switch (openItems.purpose) {
    case 'settling':
      amountPaidIsOpen.value = true
      return
    case 'cancelling':
      cancelConfirmationIsOpen.value = true
      return
    default:
      return assertNever(openItems.purpose)
  }
}

async function cancelTheSelectedItems(): Promise<void> {
  if (await openItems.cancelSelectedItems()) {
    cancelConfirmationIsOpen.value = false
  }
}

function closeThePage(): void {
  amountPaidIsOpen.value = false
  cancelConfirmationIsOpen.value = false
  openItems.dismissNotice()
}

async function closeThePageAndReload(): Promise<void> {
  closeThePage()
  await openItems.load()
}

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
    <OrderListTitle data-test="settle-title" :text="title" />
    <OrderListFilter class="mb-3" :filter="openItems.filter" @change="openItems.changeFilter" />
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
      v-if="openItems.notice !== null && !aPageIsOpen"
      class="mb-2"
      :notice="openItems.notice"
      closable
      @dismiss="openItems.dismissNotice"
    />
    <TableOrders
      :report="openItems.report"
      :selected-item-ids="openItems.selectedItemIds"
      :purpose="openItems.purpose"
      :empty-text="emptyText"
      :language="session.language"
      @toggle-item="openItems.toggleItem"
      @set-whole-table="openItems.setWholeTable"
      @set-whole-order="openItems.setWholeOrder"
    />
    <v-sheet v-if="somethingIsSelected" class="settle-footer pt-3 pb-4" data-test="settle-footer" color="background">
      <p class="selected-total app-figure mb-2" data-test="selected-total">
        {{ selectedText }}
      </p>
      <v-btn-wide
        class="open-the-page-for-the-selection"
        :data-test="footerAction.test"
        color="primary"
        @click="openThePageForTheSelection"
      >
        {{ footerAction.text }}
      </v-btn-wide>
    </v-sheet>
    <AmountPaidDialog
      v-if="amountPaidIsOpen"
      :is-settling="openItems.isSettling"
      :notice="openItems.notice"
      :selected-total-cents="openItems.selectedTotalCents"
      :language="session.language"
      @confirm="settleTheAmountPaid"
      @reload="closeThePageAndReload"
      @cancel="closeThePage"
    />
    <CancelItemsDialog
      v-if="cancelConfirmationIsOpen"
      :items="openItems.itemsToCancel"
      :cash-to-return-cents="openItems.cashToReturnForTheItemsToCancel"
      :is-cancelling="openItems.isCancelling"
      :notice="openItems.notice"
      :language="session.language"
      @confirm="cancelTheSelectedItems"
      @reload="closeThePageAndReload"
      @back="closeThePage"
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
