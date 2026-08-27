<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { isTableLabelValid } from '../core/tableLabel'
import { formatPrice } from '../core/totals'
import { useCatalogStore } from '../stores/catalog'
import { useOrderStore } from '../stores/order'
import { useSessionStore } from '../stores/session'
import { navigate } from '../router'
import LineList from '../components/review/LineList.vue'
import TableField from '../components/review/TableField.vue'
import TotalDisplay from '../components/review/TotalDisplay.vue'
import SendFailurePanel from '../components/review/SendFailurePanel.vue'
import LineStationSheet from '../components/catalog/LineStationSheet.vue'

const { t } = useI18n()
const catalog = useCatalogStore()
const order = useOrderStore()
const session = useSessionStore()

const lineAwaitingStation = ref<number | null>(null)

const tableLabel = computed({
  get: () => order.draft.tableLabel,
  set: (value: string) => order.setTable(value),
})

const canSend = computed(
  () =>
    isTableLabelValid(order.draft.tableLabel) &&
    order.basketLines.length > 0 &&
    order.sendState !== 'sending',
)

const itemAwaitingStation = computed(() => {
  if (lineAwaitingStation.value === null) {
    return null
  }
  const line = order.draft.lines[lineAwaitingStation.value]
  return catalog.catalog.items.find((item) => item.id === line?.catalogItemId) ?? null
})

const totalChanged = computed(() => order.acceptedTotalChangedTo)

function chooseStation(locationId: string): void {
  if (lineAwaitingStation.value === null) {
    return
  }
  order.chooseStation(lineAwaitingStation.value, locationId)
  lineAwaitingStation.value = null
}

async function send(): Promise<void> {
  await order.send()
}

function backToItems(): void {
  order.dismissConfirmation()
  navigate('/')
}
</script>

<template>
  <section class="review">
    <h1>{{ t('review.title') }}</h1>
    <LineList
      :lines="order.basketLines"
      :language="session.language"
      :location-name-for="catalog.locationName"
      @change-quantity="order.changeQuantity"
      @change-station="(index) => (lineAwaitingStation = index)"
      @change-note="order.noteLine"
    />
    <LineStationSheet
      v-if="itemAwaitingStation !== null"
      :item="itemAwaitingStation"
      :location-name-for="catalog.locationName"
      @choose="chooseStation"
    />
    <TableField v-model="tableLabel" :suggestions="catalog.tableSuggestions" />
    <label class="order-note">
      <span>{{ t('review.orderNote') }}</span>
      <textarea
        :value="order.draft.note ?? ''"
        @input="order.setNote(($event.target as HTMLTextAreaElement).value || null)"
      ></textarea>
    </label>
    <TotalDisplay v-if="order.sendState !== 'accepted'" :total-cents="order.totalCents" :language="session.language" />
    <button
      v-if="order.sendState !== 'accepted'"
      type="button"
      class="send"
      :disabled="!canSend"
      @click="send"
    >
      {{ order.sendState === 'sending' ? t('review.sending') : t('review.send') }}
    </button>
    <p
      v-if="order.sendState !== 'accepted' && !isTableLabelValid(order.draft.tableLabel)"
      class="table-missing"
    >
      {{ t('review.tableMissing') }}
    </p>
    <SendFailurePanel
      v-if="order.sendState === 'failed' && order.failure !== null"
      :failure="order.failure"
      @retry="send"
    />
    <template v-if="order.sendState === 'accepted' && order.acceptedOrderNumber !== null">
      <p class="sent">{{ t('review.sent', { number: order.acceptedOrderNumber }) }}</p>
      <p v-if="totalChanged !== null" class="total-changed">
        {{ t('review.totalChanged', { total: formatPrice(totalChanged, session.language) }) }}
      </p>
    </template>
    <button type="button" class="back" @click="backToItems">{{ t('review.back') }}</button>
  </section>
</template>
