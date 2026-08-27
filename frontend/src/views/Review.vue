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
  <v-container class="review">
    <h1 class="text-h5 mb-2">{{ t('review.title') }}</h1>
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
    <v-textarea
      class="order-note"
      :label="t('review.orderNote')"
      :model-value="order.draft.note ?? ''"
      @update:model-value="order.setNote($event || null)"
    />
    <TotalDisplay
      v-if="order.sendState !== 'accepted'"
      :total-cents="order.totalCents"
      :language="session.language"
    />
    <v-btn
      v-if="order.sendState !== 'accepted'"
      class="send"
      color="primary"
      block
      size="x-large"
      :disabled="!canSend"
      @click="send"
    >
      {{ order.sendState === 'sending' ? t('review.sending') : t('review.send') }}
    </v-btn>
    <v-alert
      v-if="order.sendState !== 'accepted' && !isTableLabelValid(order.draft.tableLabel)"
      class="table-missing mt-2"
      type="info"
      variant="tonal"
    >
      {{ t('review.tableMissing') }}
    </v-alert>
    <SendFailurePanel
      v-if="order.sendState === 'failed' && order.failure !== null"
      :failure="order.failure"
      @retry="send"
    />
    <template v-if="order.sendState === 'accepted' && order.acceptedOrderNumber !== null">
      <v-alert class="sent mt-4" type="success" variant="tonal">
        {{ t('review.sent', { number: order.acceptedOrderNumber }) }}
      </v-alert>
      <v-alert v-if="totalChanged !== null" class="total-changed mt-2" type="info" variant="tonal">
        {{ t('review.totalChanged', { total: formatPrice(totalChanged, session.language) }) }}
      </v-alert>
    </template>
    <v-btn class="back mt-4" variant="text" block @click="backToItems">
      {{ t('review.back') }}
    </v-btn>
  </v-container>
</template>
