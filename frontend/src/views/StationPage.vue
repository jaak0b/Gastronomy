<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useStationStore, TAKE_DELAY_SECONDS } from '../stores/station'
import { useConnectionStore } from '../stores/connection'
import StationFilter from '../components/station/StationFilter.vue'
import StationScreenOrderRow from '../components/station/StationScreenOrderRow.vue'

const { t } = useI18n()
const station = useStationStore()
const connection = useConnectionStore()

const selectedName = computed(
  () =>
    station.stations.find((candidate) => candidate.stationId === station.selectedStationId)
      ?.name ?? '',
)

const releases: (() => void)[] = []

onMounted(async () => {
  await station.open()
  releases.push(
    station.listen(),
    connection.onEvent('StationBacklogChanged', () => {
      void station.loadTickets()
    }),
    connection.onEvent('TicketStatusChanged', () => {
      void station.loadTickets()
    }),
    connection.onEvent('PrinterStatusChanged', () => {
      void station.refresh()
    }),
  )
})

onUnmounted(() => {
  for (const release of releases) {
    release()
  }
  releases.length = 0
})
</script>

<template>
  <v-container v-if="station.loadFailed" class="station-page-failed">
    <v-alert class="error" type="error" variant="tonal">{{ t('admin.loadFailed') }}</v-alert>
  </v-container>
  <v-container v-else class="station-page">
    <h1 class="text-h5 mb-2">{{ t('station.title', { name: selectedName }) }}</h1>
    <StationFilter
      :stations="station.stations"
      :selected-station-id="station.selectedStationId"
      @select="station.selectStation"
    />
    <v-alert v-if="station.printer === null" class="no-printer mb-2" type="info" variant="tonal">
      {{ t('station.noPrinter') }}
    </v-alert>
    <v-alert v-if="station.stationOrders.length === 0" class="empty" type="info" variant="tonal">
      {{ t('station.empty') }}
    </v-alert>
    <StationScreenOrderRow
      v-for="stationOrder in station.stationOrders"
      :key="stationOrder.stationOrderId"
      :station-order="stationOrder"
      :is-pending="station.isPending(stationOrder.stationOrderId)"
      :take-delay-seconds="TAKE_DELAY_SECONDS"
      :notice-key="station.noticeKeyByTicketId[stationOrder.stationOrderId] ?? null"
      @take="station.beginTake(stationOrder.stationOrderId)"
      @undo="station.undoTake(stationOrder.stationOrderId)"
    />
  </v-container>
</template>
