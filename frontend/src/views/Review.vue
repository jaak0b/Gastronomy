<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { isTableNameValid } from '../core/tableName'
import { formatPrice } from '../core/totals'
import { useEstimatesStore } from '../stores/estimates'
import { useOrderStore } from '../stores/order'
import { useSessionStore } from '../stores/session'
import { navigate } from '../router'
import DockedStrip from '../components/DockedStrip.vue'
import LineList from '../components/review/LineList.vue'
import SendFailurePanel from '../components/review/SendFailurePanel.vue'
import SendFailedTwiceDialog from '../components/review/SendFailedTwiceDialog.vue'
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

const settleOnSendAwaitingConfirmation = ref<boolean | null>(null)

function askWhetherToSend(settleOnSend: boolean): void {
  settleOnSendAwaitingConfirmation.value = settleOnSend
}

function keepTheOrderOnTheScreen(): void {
  settleOnSendAwaitingConfirmation.value = null
}

async function sendAsConfirmed(): Promise<void> {
  const settleOnSend = settleOnSendAwaitingConfirmation.value
  settleOnSendAwaitingConfirmation.value = null
  if (settleOnSend !== null) {
    await send(settleOnSend)
  }
}

async function send(settleOnSend: boolean): Promise<void> {
  await order.send(settleOnSend)
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
    <h1 class="text-h5">{{ t('review.title') }}</h1>
    <div class="order-heading d-flex align-baseline mb-4">
      <p class="table-name text-subtitle-1 text-medium-emphasis">
        {{ t('review.tableIs', { name: order.draft.tableName }) }}
      </p>
      <span class="order-total text-h5">{{ total }}</span>
    </div>
    <v-alert v-if="estimates.loadFailed" class="estimates-failed mb-2" type="info" variant="tonal">
      {{ t('estimates.loadFailed') }}
    </v-alert>
    <LineList
      :lines="order.basketLines"
      :order-note="order.draft.note"
      :language="session.language"
      :estimates="estimates.stations"
      :delivery-mode-for="order.deliveryModeAt"
      :changes-are-refused="order.changesAreRefused"
      @choose-delivery-mode="order.chooseDeliveryMode"
    />
    <SendFailurePanel
      v-if="order.sendHasFailed && order.failure !== null"
      :failure="order.failure"
    />
    <DockedStrip class="review-footer">
      <div class="pt-3 pb-4">
        <v-btn
          v-if="order.changesAreRefused"
          class="send-again mt-2"
          color="primary"
          block
          size="x-large"
          :disabled="order.isSending"
          @click="sendAgain"
        >
          {{ order.isSending ? t('review.sending') : t('review.retry') }}
        </v-btn>
        <v-btn
          v-else-if="order.hasLinesThatCannotBeOrdered"
          class="drop-lines-that-cannot-be-ordered mt-2"
          color="warning"
          variant="outlined"
          block
          size="x-large"
          @click="order.dropLinesThatCannotBeOrdered"
        >
          {{ t('review.removeLinesThatCannotBeOrdered') }}
        </v-btn>
        <template v-else>
          <v-btn
            class="send-and-settle mt-2"
            color="primary"
            block
            size="x-large"
            :disabled="!canSend"
            @click="askWhetherToSend(true)"
          >
            {{ order.isSending ? t('review.sending') : t('review.sendAndSettle') }}
          </v-btn>
          <v-btn
            class="send mt-2"
            color="primary"
            variant="outlined"
            block
            size="x-large"
            :disabled="!canSend"
            @click="askWhetherToSend(false)"
          >
            {{ order.isSending ? t('review.sending') : t('review.send') }}
          </v-btn>
        </template>
        <v-divider class="mt-5" />
        <v-btn
          class="back mt-4"
          variant="outlined"
          block
          :disabled="order.changesAreRefused"
          @click="backToItems"
        >
          {{ t('review.back') }}
        </v-btn>
      </div>
    </DockedStrip>
    <ConfirmSendDialog
      v-if="settleOnSendAwaitingConfirmation !== null"
      :settle-on-send="settleOnSendAwaitingConfirmation"
      :table-name="order.draft.tableName"
      :total-cents="order.totalCents"
      :language="session.language"
      :lines="order.basketLines"
      :estimates="estimates.stations"
      :delivery-mode-for="order.deliveryModeAt"
      @confirmed="sendAsConfirmed"
      @cancelled="keepTheOrderOnTheScreen"
    />
    <SendFailedTwiceDialog
      v-if="order.onlyPaperIsLeft"
      @written-down="startTheNextOrder"
      @try-again="sendAgain"
    />
  </v-container>
</template>

<style scoped>
.order-heading {
  gap: 1rem;
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
