<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ConfirmedSettlement } from '../../shared/api/apiTypes'
import { isTableNameValid } from '../core/tableName'
import { formatPrice } from '../core/totals'
import { useEstimatesStore } from '../stores/estimates'
import { useOrderStore } from '../stores/order'
import { useSessionStore } from '../../shared/stores/session'
import { navigate } from '../../shared/router/router'
import DockedStrip from '../components/DockedStrip.vue'
import LineList from '../components/review/LineList.vue'
import SendFailurePanel from '../components/review/SendFailurePanel.vue'
import ConfirmSendDialog from '../components/review/ConfirmSendDialog.vue'

const { t } = useI18n()
const estimates = useEstimatesStore()
const order = useOrderStore()
const session = useSessionStore()

onMounted(async () => {
  await estimates.load()
})

const total = computed(() => formatPrice(order.totalCents, session.language))

const canSend = computed(
  () =>
    isTableNameValid(order.draft.tableName) &&
    order.basketLines.length > 0 &&
    !order.isSending,
)

const sendSheetIsOpen = ref(false)

function openTheSendSheet(): void {
  sendSheetIsOpen.value = true
}

function keepTheOrderOnTheScreen(): void {
  sendSheetIsOpen.value = false
}

async function sendAsConfirmed(settlement: ConfirmedSettlement | null): Promise<void> {
  sendSheetIsOpen.value = false
  await order.send(settlement)
  if (order.sendState === 'accepted') {
    navigate('/')
  }
}

async function sendAgain(): Promise<void> {
  await order.sendAgain()
  if (order.sendState === 'accepted') {
    navigate('/')
  }
}

function startTheNextOrder(): void {
  order.startNextOrderAfterWritingItDown()
  navigate('/')
}

function backToItems(): void {
  order.dismissConfirmation()
  navigate('/')
}
</script>

<template>
  <v-container class="review">
    <v-alert
      v-if="order.onlyWritingItDownIsLeft"
      class="write-it-down my-4"
      type="warning"
      variant="tonal"
    >
      <p class="may-have-arrived">{{ t('review.sendStillUnknown') }}</p>
      <p class="instruction mb-0">{{ t('review.writeItDown') }}</p>
    </v-alert>
    <SendFailurePanel
      v-else-if="order.sendHasFailed && order.failure !== null"
      :failure="order.failure"
    />
    <div class="review-heading d-flex align-center mb-2">
      <h1 class="table-name text-subtitle-1 text-medium-emphasis">
        {{ t('review.tableIs', { name: order.draft.tableName }) }}
      </h1>
      <span class="order-total text-h5">{{ total }}</span>
    </div>
    <LineList
      :lines="order.basketLines"
      :language="session.language"
      :estimates="estimates.stations"
      :delivery-mode-for="order.deliveryModeAt"
      :changes-are-refused="order.changesAreRefused"
      @choose-delivery-mode="order.chooseDeliveryMode"
    />
    <DockedStrip class="review-footer">
      <div class="pt-3 pb-4">
        <template v-if="order.changesAreRefused">
          <v-btn
            v-if="!order.isSending"
            class="written-down mt-2"
            color="primary"
            block
            size="x-large"
            @click="startTheNextOrder"
          >
            {{ t('review.writtenDown') }}
          </v-btn>
          <v-btn
            class="send-again mt-2"
            color="primary"
            variant="outlined"
            block
            size="x-large"
            :disabled="order.isSending"
            @click="sendAgain"
          >
            {{ order.isSending ? t('review.sending') : t('review.retry') }}
          </v-btn>
        </template>
        <v-btn
          v-else-if="order.hasLinesThatCannotBeOrdered"
          class="drop-lines-that-cannot-be-ordered mt-2"
          color="primary"
          variant="outlined"
          block
          size="x-large"
          @click="order.dropLinesThatCannotBeOrdered"
        >
          {{ t('review.removeLinesThatCannotBeOrdered') }}
        </v-btn>
        <template v-else>
          <v-btn
            class="continue mt-2"
            color="primary"
            block
            size="x-large"
            :disabled="!canSend"
            @click="openTheSendSheet"
          >
            {{ t('review.continue') }}
          </v-btn>
        </template>
        <v-btn
          v-if="!order.changesAreRefused"
          class="back mt-2"
          variant="text"
          block
          @click="backToItems"
        >
          {{ t('review.back') }}
        </v-btn>
      </div>
    </DockedStrip>
    <ConfirmSendDialog
      v-if="sendSheetIsOpen"
      :table-name="order.draft.tableName"
      :total-cents="order.totalCents"
      :language="session.language"
      :lines="order.basketLines"
      :estimates="estimates.stations"
      :delivery-mode-for="order.deliveryModeAt"
      @confirmed="sendAsConfirmed"
      @cancelled="keepTheOrderOnTheScreen"
    />
  </v-container>
</template>

<style scoped>
.review {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
  min-height: 100dvh;
  padding-bottom: 0;
}

.review-footer {
  margin-top: auto;
}

.review-heading {
  position: sticky;
  top: 0;
  z-index: 1;
  background: rgb(var(--v-theme-surface));
  padding-inline: 1rem;
}

.table-name {
  flex: 1 1 auto;
  min-width: 0;
  margin: 0;
  overflow-wrap: anywhere;
}

.order-total {
  flex: 0 0 auto;
  white-space: nowrap;
}
</style>
