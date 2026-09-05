<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { isTableNameValid } from '../core/tableName'
import { useCatalogStore } from '../stores/catalog'
import { useOrderStore } from '../stores/order'
import { useSessionStore } from '../stores/session'
import { navigate } from '../router'
import LineList from '../components/review/LineList.vue'
import TotalDisplay from '../components/review/TotalDisplay.vue'
import SendFailurePanel from '../components/review/SendFailurePanel.vue'

const { t } = useI18n()
const catalog = useCatalogStore()
const order = useOrderStore()
const session = useSessionStore()

const canSend = computed(
  () =>
    isTableNameValid(order.draft.tableName) &&
    order.basketLines.length > 0 &&
    order.sendState !== 'sending',
)

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

function backToItems(): void {
  order.dismissConfirmation()
  navigate('/')
}
</script>

<template>
  <v-container class="review">
    <h1 class="text-h5">{{ t('review.title') }}</h1>
    <p class="table-name text-subtitle-1 text-medium-emphasis mb-4">
      {{ t('review.tableIs', { name: order.draft.tableName }) }}
    </p>
    <LineList
      :lines="order.basketLines"
      :order-note="order.draft.note"
      :language="session.language"
      :station-name-for="catalog.stationName"
    />
    <v-btn
      v-if="order.hasLinesNoLongerOnTheMenu"
      class="drop-lines-no-longer-on-the-menu mt-2"
      color="warning"
      variant="outlined"
      block
      size="large"
      @click="order.dropLinesNoLongerOnTheMenu"
    >
      {{ t('review.removeLinesNoLongerOnTheMenu') }}
    </v-btn>
    <SendFailurePanel
      v-if="order.sendState === 'failed' && order.failure !== null"
      :failure="order.failure"
      @retry="sendAgain"
    />
    <v-btn class="back mt-2 mb-4" variant="text" block @click="backToItems">
      {{ t('review.back') }}
    </v-btn>
    <v-sheet class="review-footer pt-3 pb-4" color="background">
      <TotalDisplay :total-cents="order.totalCents" :language="session.language" />
      <template v-if="order.sendState !== 'failed'">
        <v-btn
          class="send-and-settle mt-2"
          color="primary"
          block
          size="x-large"
          :disabled="!canSend"
          @click="send(true)"
        >
          {{ order.sendState === 'sending' ? t('review.sending') : t('review.sendAndSettle') }}
        </v-btn>
        <v-btn
          class="send mt-2"
          color="primary"
          variant="outlined"
          block
          size="x-large"
          :disabled="!canSend"
          @click="send(false)"
        >
          {{ order.sendState === 'sending' ? t('review.sending') : t('review.send') }}
        </v-btn>
      </template>
    </v-sheet>
  </v-container>
</template>

<style scoped>
.review-footer {
  position: sticky;
  bottom: 0;
  z-index: 2;
  border-top: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
