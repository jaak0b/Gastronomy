<script setup lang="ts">
import { computed, onUnmounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { NOTHING_TO_LOAD, showsTheEmptyListText } from '../../shared/core/emptyList'
import { formatPrice } from '../../shared/core/money'
import { quoteLinesFor } from '../core/estimates'
import { useEstimatesStore } from '../stores/estimates'
import { useOrderStore } from '../stores/order'
import { useSessionStore } from '../../shared/stores/session'
import { navigate } from '../../shared/router/router'
import { useDraftTableName } from '../composables/useDraftTableName'
import { useSendAndLeave } from '../composables/useSendAndLeave'
import BaseEmptyListText from '../../shared/components/BaseEmptyListText.vue'
import DockedStrip from '../components/DockedStrip.vue'
import LineList from '../components/review/LineList.vue'
import SendFailurePanel from '../components/review/SendFailurePanel.vue'

const { t } = useI18n()
const estimates = useEstimatesStore()
const order = useOrderStore()
const session = useSessionStore()
const { send, sendAgain } = useSendAndLeave()
const { draftTableName } = useDraftTableName()

const quoteLines = computed(() => quoteLinesFor(order.basketLines))

watch(
  quoteLines,
  (lines) => {
    void estimates.quote(lines)
  },
  { immediate: true, deep: true },
)

onUnmounted(() => {
  estimates.stopQuoting()
})

const total = computed(() => formatPrice(order.totalCents, session.language))

const showsEmptyListText = computed(() =>
  showsTheEmptyListText(NOTHING_TO_LOAD, order.basketLines.length),
)

const canSend = computed(
  () => order.draft.table !== null && order.basketLines.length > 0 && !order.isSending,
)

function startTheNextOrder(): void {
  order.startNextOrderAfterWritingItDown()
  navigate('/')
}

function backToItems(): void {
  order.dismissConfirmation()
  navigate('/catalog')
}
</script>

<template>
  <v-container class="review" data-test="review">
    <v-alert
      v-if="order.onlyWritingItDownIsLeft"
      class="write-it-down my-4"
      data-test="write-it-down"
      type="warning"
      variant="tonal"
    >
      <p class="may-have-arrived">{{ t('phone.review.messages.sendStillUnknown') }}</p>
      <p class="instruction mb-0" data-test="instruction">{{ t('phone.review.messages.writeItDown') }}</p>
    </v-alert>
    <SendFailurePanel
      v-else-if="order.sendHasFailed && order.failure !== null"
      :failure="order.failure"
    />
    <div class="review-heading d-flex align-center mb-2" data-test="review-heading">
      <h1 class="table-name app-text text-medium-emphasis" data-test="table-name">
        <template v-if="draftTableName !== null">
          {{ t('phone.review.labels.table', { name: draftTableName }) }}
        </template>
      </h1>
      <span class="order-total app-figure" data-test="order-total">{{ total }}</span>
    </div>
    <BaseEmptyListText v-if="showsEmptyListText" class="my-2" data-test="no-lines" />
    <LineList
      :lines="order.basketLines"
      :language="session.language"
      :quoted-stations="estimates.quotedStations"
      :delivery-mode-for="order.deliveryModeAt"
      :changes-are-refused="order.changesAreRefused"
      @choose-delivery-mode="order.chooseDeliveryMode"
    />
    <DockedStrip class="review-footer">
      <div class="pt-3 pb-4">
        <template v-if="order.changesAreRefused">
          <v-btn-wide
            v-if="order.onlyWritingItDownIsLeft"
            class="written-down mt-2"
            data-test="written-down"
            color="primary"
            @click="startTheNextOrder"
          >
            {{ t('phone.review.actions.writtenDown') }}
          </v-btn-wide>
          <v-btn-wide
            class="send-again mt-2"
            data-test="send-again"
            color="primary"
            variant="outlined"
            :disabled="order.isSending"
            @click="sendAgain"
          >
            {{ order.isSending ? t('phone.review.actions.sending') : t('phone.review.actions.retry') }}
          </v-btn-wide>
        </template>
        <v-btn-wide
          v-else-if="order.hasLinesThatCannotBeOrdered"
          class="drop-lines-that-cannot-be-ordered mt-2"
          data-test="drop-lines-that-cannot-be-ordered"
          color="primary"
          variant="outlined"
          @click="order.dropLinesThatCannotBeOrdered"
        >
          {{ t('phone.review.actions.removeLinesThatCannotBeOrdered') }}
        </v-btn-wide>
        <template v-else>
          <v-btn-wide
            class="send-and-settle send-button mt-3"
            data-test="send-and-settle"
            color="primary"
            :disabled="!canSend"
            @click="send('settleRightAway')"
          >
            {{ t('phone.review.actions.sendAndSettle') }}
          </v-btn-wide>
          <v-btn-wide
            class="send-and-settle-later send-button mt-2"
            data-test="send-and-settle-later"
            color="primary"
            variant="outlined"
            :disabled="!canSend"
            @click="send('leaveOpen')"
          >
            {{ t('phone.review.actions.sendAndSettleLater') }}
          </v-btn-wide>
        </template>
        <v-btn-wide
          v-if="!order.changesAreRefused"
          class="back mt-2"
          data-test="back"
          variant="text"
          @click="backToItems"
        >
          {{ t('common.actions.back') }}
        </v-btn-wide>
      </div>
    </DockedStrip>
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

.send-button {
  height: auto;
  min-height: 3.5rem;
  padding-block: 0.75rem;
  text-transform: none;
  letter-spacing: normal;
}

.send-button :deep(.v-btn__content) {
  white-space: normal;
}

.order-total {
  flex: 0 0 auto;
  white-space: nowrap;
}
</style>
