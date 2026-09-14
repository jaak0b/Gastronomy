<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import type { StationSlice } from '../core/apiTypes'
import { selectedUnits, stationStats, type ItemUnits } from '../core/stationBoard'
import { useStationStore } from '../stores/station'
import { useSessionStore } from '../stores/session'
import LanguageSwitch from '../components/LanguageSwitch.vue'
import StationSliceCard from '../components/station/StationSliceCard.vue'
import StationFulfilledCard from '../components/station/StationFulfilledCard.vue'
import StationDoneDialog from '../components/station/StationDoneDialog.vue'

const { t } = useI18n()
const station = useStationStore()
const session = useSessionStore()
let stopListening: (() => void) | null = null

const stats = computed(() => stationStats(station.orders))
const doneSlice = ref<StationSlice | null>(null)
const doneItemIds = ref<string[]>([])
const doneUnits = computed<ItemUnits[]>(() =>
  doneSlice.value === null ? [] : selectedUnits([doneSlice.value], doneItemIds.value),
)

const stationName = computed(() => station.station?.name ?? session.station?.name ?? '')

onMounted(async () => {
  stopListening = station.listen()
  await station.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})

function openDone(slice: StationSlice, orderItemIds: string[]): void {
  doneSlice.value = slice
  doneItemIds.value = orderItemIds
}

function closeDone(): void {
  doneSlice.value = null
  doneItemIds.value = []
}

async function confirmDone(): Promise<void> {
  const orderItemIds = doneItemIds.value
  closeDone()
  await station.fulfill(orderItemIds)
}
</script>

<template>
  <v-container fluid class="station-page">
    <header class="station-header d-flex align-center ga-4 mb-4">
      <h1 class="station-name text-h4 flex-grow-1">{{ stationName }}</h1>
      <LanguageSwitch
        :language="session.language"
        label-key="station.language"
        @select="session.setLanguage"
      />
    </header>

    <v-alert v-if="station.loadFailed" class="load-failed mb-4" type="warning" variant="tonal">
      {{ t(station.loadFailureKey ?? 'station.loadFailed') }}
    </v-alert>
    <v-alert
      v-if="station.failureKey !== null"
      class="action-failed mb-4"
      type="warning"
      variant="tonal"
    >
      {{ t(station.failureKey) }}
    </v-alert>

    <template v-if="station.isShowingFulfilled">
      <div class="done-head d-flex align-center ga-3 mb-4">
        <h2 class="done-heading text-h5 flex-grow-1">{{ t('station.doneHeading') }}</h2>
        <v-btn class="back-to-queue" variant="outlined" size="large" @click="station.closeFulfilled">
          {{ t('station.backToQueue') }}
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
        v-for="slice in station.fulfilled"
        :key="slice.stationOrderId"
        :slice="slice"
        :is-working="station.isWorking"
        @put-back="station.unfulfill"
      />
    </template>

    <template v-else>
      <section v-if="station.hasWork" class="station-stats mb-4">
        <h2 class="stats-heading text-h6 mb-1">{{ t('station.statsHeading') }}</h2>
        <p class="stat-together text-body-1 mb-0">
          {{ t('station.statsTogether', { count: stats.togetherOrders }, stats.togetherOrders) }}
        </p>
        <p class="stat-as-it-comes text-body-1 mb-1">
          {{
            t('station.statsAsItComes', { count: stats.asItComesOrders }, stats.asItComesOrders)
          }}
        </p>
        <div class="stat-items d-flex flex-wrap ga-4">
          <span v-for="unit in stats.openUnits" :key="unit.itemName" class="stat-item">
            {{ t('station.itemUnits', { count: unit.units, item: unit.itemName }) }}
          </span>
        </div>
      </section>

      <v-btn
        class="show-done mb-4"
        color="primary"
        variant="outlined"
        size="large"
        @click="station.openFulfilled"
      >
        {{ t('station.showDone') }}
      </v-btn>

      <v-row>
        <v-col cols="12" md="6" class="orders-column">
          <h2 class="orders-heading text-h6 mb-2">{{ t('station.ordersHeading') }}</h2>
          <StationSliceCard
            v-for="slice in station.orders"
            :key="slice.stationOrderId"
            :slice="slice"
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
          <StationSliceCard
            v-for="slice in station.asItComes"
            :key="slice.stationOrderId"
            :slice="slice"
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
      v-if="doneSlice !== null"
      :table-name="doneSlice.tableName"
      :units="doneUnits"
      @confirmed="confirmDone"
      @cancelled="closeDone"
    />
  </v-container>
</template>
