<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationOrder } from '../core/apiTypes'
import { selectedUnits, stationStats, type ItemLine } from '../core/stationBoard'
import { useStationStore } from '../stores/station'
import { useSessionStore } from '../stores/session'
import LanguageSwitch from '../components/LanguageSwitch.vue'
import StationOrderCard from '../components/station/StationOrderCard.vue'
import StationFulfilledCard from '../components/station/StationFulfilledCard.vue'
import StationDoneDialog from '../components/station/StationDoneDialog.vue'
import StationOpenBoard from '../components/station/StationOpenBoard.vue'

const { t } = useI18n()
const station = useStationStore()
const session = useSessionStore()
let stopListening: (() => void) | null = null

const stats = computed(() => stationStats(station.orders))
const doneStationOrder = ref<StationOrder | null>(null)
const doneItemIds = ref<string[]>([])
const isShowingOverview = ref(false)
const doneUnits = computed<ItemLine[]>(() =>
  doneStationOrder.value === null
    ? []
    : selectedUnits([doneStationOrder.value], doneItemIds.value),
)

const stationName = computed(() => station.station?.name ?? session.station?.name ?? '')

const failureText = computed<string | null>(() => {
  if (station.loadFailed) {
    return t(station.loadFailureKey ?? 'station.loadFailed')
  }
  return station.failureKey === null ? null : t(station.failureKey)
})

onMounted(async () => {
  stopListening = station.listen()
  await station.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})

function openDone(stationOrder: StationOrder, orderItemIds: string[]): void {
  doneStationOrder.value = stationOrder
  doneItemIds.value = orderItemIds
}

function closeDone(): void {
  doneStationOrder.value = null
  doneItemIds.value = []
}

async function confirmDone(): Promise<void> {
  const orderItemIds = doneItemIds.value
  closeDone()
  await station.fulfill(orderItemIds)
}

function openOverview(): void {
  isShowingOverview.value = true
}

function closeOverview(): void {
  isShowingOverview.value = false
}
</script>

<template>
  <v-container fluid class="station-page">
    <header class="station-header d-flex align-center ga-3 mb-4">
      <h1 class="station-name text-h5">{{ stationName }}</h1>
      <v-spacer />
      <LanguageSwitch :language="session.language" @select="session.setLanguage" />
      <template v-if="!isShowingOverview && !station.isShowingFulfilled">
        <v-btn class="show-overview" variant="outlined" size="large" @click="openOverview">
          {{ t('station.overview') }}
        </v-btn>
        <v-btn class="show-done" variant="outlined" size="large" @click="station.openFulfilled">
          {{ t('station.showDone') }}
        </v-btn>
      </template>
    </header>

    <v-alert
      v-if="!isShowingOverview && station.loadFailed"
      class="load-failed mb-4"
      type="warning"
      variant="tonal"
    >
      {{ t(station.loadFailureKey ?? 'station.loadFailed') }}
    </v-alert>
    <v-alert
      v-if="!isShowingOverview && station.failureKey !== null"
      class="action-failed mb-4"
      type="warning"
      variant="tonal"
    >
      {{ t(station.failureKey) }}
    </v-alert>

    <StationOpenBoard
      v-if="isShowingOverview"
      :together-count="stats.togetherOrders"
      :as-it-comes-count="stats.asItComesOrders"
      :lines="stats.openLines"
      :failure-text="failureText"
      @close="closeOverview"
    />

    <template v-else-if="station.isShowingFulfilled">
      <div class="done-head d-flex align-center ga-3 mb-4">
        <h2 class="done-heading text-h5 flex-grow-1">{{ t('station.doneHeading') }}</h2>
        <v-btn
          class="back-to-orders"
          variant="outlined"
          size="large"
          @click="station.closeFulfilled"
        >
          {{ t('station.backToOrders') }}
        </v-btn>
      </div>
      <v-alert
        v-if="station.fulfilledLoadFailed"
        class="load-failed mb-4"
        type="warning"
        variant="tonal"
      >
        {{ t('station.loadFailed') }}
      </v-alert>
      <v-alert v-if="station.hasNothingDone" class="nothing-done mb-4" type="info" variant="tonal">
        {{ t('station.nothingDone') }}
      </v-alert>
      <StationFulfilledCard
        v-for="stationOrder in station.fulfilled"
        :key="stationOrder.stationOrderId"
        :station-order="stationOrder"
        :is-working="station.isWorking"
        @put-back="station.unfulfill"
      />
    </template>

    <template v-else>
      <v-row>
        <v-col cols="12" md="6" class="orders-column">
          <h2 class="orders-heading text-h6 mb-2">{{ t('station.ordersHeading') }}</h2>
          <StationOrderCard
            v-for="stationOrder in station.orders"
            :key="stationOrder.stationOrderId"
            :station-order="stationOrder"
            :selected-item-ids="station.selectedItemIds"
            :is-working="station.isWorking"
            :show-hide="false"
            @toggle-item="station.toggleItemSelection"
            @fulfil="openDone"
            @hide="station.hide"
          />
        </v-col>
        <v-col cols="12" md="6" class="as-it-comes-column">
          <h2 class="as-it-comes-heading text-h6 mb-2">
            {{ t('station.asItComesHeading') }}
          </h2>
          <StationOrderCard
            v-for="stationOrder in station.asItComes"
            :key="stationOrder.stationOrderId"
            :station-order="stationOrder"
            :selected-item-ids="station.selectedItemIds"
            :is-working="station.isWorking"
            :show-hide="true"
            @toggle-item="station.toggleItemSelection"
            @fulfil="openDone"
            @hide="station.hide"
          />
        </v-col>
      </v-row>
    </template>

    <StationDoneDialog
      v-if="doneStationOrder !== null"
      :table-name="doneStationOrder.tableName"
      :lines="doneUnits"
      @confirmed="confirmDone"
      @cancelled="closeDone"
    />
  </v-container>
</template>

<style scoped>
.station-header {
  flex-wrap: wrap;
}

.station-header .station-name {
  min-width: 0;
  overflow-wrap: anywhere;
}
</style>
