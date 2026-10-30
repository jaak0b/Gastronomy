<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { showsTheEmptyListText } from '../../shared/core/emptyList'
import { stationStats } from '../../shared/core/stationBoard'
import { useSessionStore } from '../../shared/stores/session'
import BaseEmptyListText from '../../shared/components/BaseEmptyListText.vue'
import LanguageSwitch from '../../shared/components/LanguageSwitch.vue'
import { useStationDoneDialog } from '../composables/useStationDoneDialog'
import { useStationStore } from '../stores/station'
import StationOrderCard from '../components/StationOrderCard.vue'
import StationFulfilledCard from '../components/StationFulfilledCard.vue'
import StationDoneDialog from '../components/StationDoneDialog.vue'
import StationOpenBoard from '../components/StationOpenBoard.vue'
import { STATION_DEFAULTS, STATION_SIZES_CLASS } from '../styles/stationSizes'
import '../styles/stationSizes.css'

const { t } = useI18n()
const station = useStationStore()
const session = useSessionStore()
const { doneStationOrder, doneUnits, openDone, closeDone, confirmDone } = useStationDoneDialog()
let stopListening: (() => void) | null = null

const stats = computed(() => stationStats(station.orders))
const isShowingOverview = ref(false)

const ordersAreEmpty = computed(() => showsTheEmptyListText(station, station.orders.length))
const asItComesOrdersAreEmpty = computed(() => showsTheEmptyListText(station, station.asItComes.length))
const openLinesAreEmpty = computed(() => showsTheEmptyListText(station, stats.value.openLines.length))

const stationName = computed(() => station.identity?.name ?? session.station?.name ?? '')

const failureText = computed<string | null>(() => {
  if (station.loadFailed) {
    return t(station.loadFailureKey ?? 'station.board.errors.loadFailed')
  }
  return station.failure === null ? null : t(station.failure.key, station.failure.parameters)
})

onMounted(async () => {
  stopListening = station.listen()
  await station.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})

function openOverview(): void {
  isShowingOverview.value = true
}

function closeOverview(): void {
  isShowingOverview.value = false
}
</script>

<template>
  <v-defaults-provider :defaults="STATION_DEFAULTS">
    <v-container fluid class="station-page" data-test="station-page" :class="STATION_SIZES_CLASS">
      <header class="station-header d-flex align-center ga-3 mb-4">
        <h1 class="station-name app-page-title" data-test="station-name">{{ stationName }}</h1>
        <v-spacer />
        <LanguageSwitch :language="session.language" @select="session.setLanguage" />
        <template v-if="!isShowingOverview && !station.isShowingFulfilled">
          <v-btn class="show-overview" data-test="show-overview" variant="outlined" @click="openOverview">
            {{ t('station.board.actions.overview') }}
          </v-btn>
          <v-btn class="show-done" data-test="show-done" variant="outlined" @click="station.openFulfilled">
            {{ t('station.board.actions.showDone') }}
          </v-btn>
        </template>
      </header>

      <v-alert
        v-if="!isShowingOverview && station.loadFailed"
        class="load-failed mb-4"
        data-test="load-failed"
        type="warning"
        variant="tonal"
      >
        {{ t(station.loadFailureKey ?? 'station.board.errors.loadFailed') }}
      </v-alert>
      <v-alert
        v-if="!isShowingOverview && station.failure !== null"
        class="action-failed mb-4"
        data-test="action-failed"
        type="warning"
        variant="tonal"
      >
        {{ t(station.failure.key, station.failure.parameters) }}
      </v-alert>

      <StationOpenBoard
        v-if="isShowingOverview"
        :together-count="stats.togetherOrders"
        :as-it-comes-count="stats.asItComesOrders"
        :lines="stats.openLines"
        :shows-empty-list-text="openLinesAreEmpty"
        :failure-text="failureText"
        @close="closeOverview"
      />

      <template v-else-if="station.isShowingFulfilled">
        <div class="done-head d-flex align-center ga-3 mb-4">
          <h2 class="done-heading app-section-title flex-grow-1" data-test="done-heading">{{ t('station.done.title') }}</h2>
          <v-btn
            class="back-to-orders"
            data-test="back-to-orders"
            variant="outlined"
            @click="station.closeFulfilled"
          >
            {{ t('station.board.actions.backToOrders') }}
          </v-btn>
        </div>
        <v-alert
          v-if="station.fulfilledLoadFailed"
          class="load-failed mb-4"
          data-test="load-failed"
          type="warning"
          variant="tonal"
        >
          {{ t('station.board.errors.loadFailed') }}
        </v-alert>
        <BaseEmptyListText v-if="station.hasNothingDone" class="nothing-done mb-4" data-test="nothing-done" />
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
          <v-col cols="12" md="6" class="orders-column" data-test="orders-column">
            <h2 class="orders-heading app-section-title mb-2" data-test="orders-heading">{{ t('station.board.labels.ordersHeading') }}</h2>
            <BaseEmptyListText v-if="ordersAreEmpty" />
            <StationOrderCard
              v-for="stationOrder in station.orders"
              :key="stationOrder.stationOrderId"
              :station-order="stationOrder"
              :selected-item-ids="station.selectedItemIds"
              :is-working="station.isWorking"
              :show-hide="false"
              @toggle-item="station.toggleItemSelection"
              @fulfill="openDone"
              @acknowledge="station.acknowledgeCancellation"
              @hide="station.hide"
            />
          </v-col>
          <v-col cols="12" md="6" class="as-it-comes-column" data-test="as-it-comes-column">
            <h2 class="as-it-comes-heading app-section-title mb-2">
              {{ t('station.board.labels.asItComesHeading') }}
            </h2>
            <BaseEmptyListText v-if="asItComesOrdersAreEmpty" />
            <StationOrderCard
              v-for="stationOrder in station.asItComes"
              :key="stationOrder.stationOrderId"
              :station-order="stationOrder"
              :selected-item-ids="station.selectedItemIds"
              :is-working="station.isWorking"
              :show-hide="true"
              @toggle-item="station.toggleItemSelection"
              @fulfill="openDone"
              @acknowledge="station.acknowledgeCancellation"
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
  </v-defaults-provider>
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
