<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'
import { useStationStore, TAKE_DELAY_SECONDS } from '../stores/station'
import { useConnectionStore } from '../stores/connection'
import StationWarning from '../components/station/StationWarning.vue'
import StationFilter from '../components/station/StationFilter.vue'
import StationTicketRow from '../components/station/StationTicketRow.vue'

const { t } = useI18n()
const station = useStationStore()
const connection = useConnectionStore()

const selectedName = computed(
  () =>
    station.locations.find((location) => location.locationId === station.selectedLocationId)
      ?.name ?? '',
)

const printerIsBack = computed(
  () =>
    station.printer !== null &&
    station.printer.isOnline &&
    !station.printer.isFaulty &&
    !station.printer.isPaperEnd &&
    !station.printer.isCoverOpen,
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
  <p v-if="station.loadFailed" class="error">{{ t('admin.loadFailed') }}</p>
  <section v-else class="station-page">
    <h1>{{ t('station.title', { name: selectedName }) }}</h1>
    <StationWarning />
    <StationFilter
      :locations="station.locations"
      :selected-location-id="station.selectedLocationId"
      @select="station.selectLocation"
    />
    <p v-if="printerIsBack" class="printer-back">{{ t('station.printerBack') }}</p>
    <p v-if="station.tickets.length === 0" class="empty">{{ t('station.empty') }}</p>
    <StationTicketRow
      v-for="ticket in station.tickets"
      :key="ticket.ticketId"
      :ticket="ticket"
      :is-pending="station.isPending(ticket.ticketId)"
      :take-delay-seconds="TAKE_DELAY_SECONDS"
      :notice-key="station.noticeKeyByTicketId[ticket.ticketId] ?? null"
      @take="station.beginTake(ticket.ticketId)"
      @undo="station.undoTake(ticket.ticketId)"
    />
  </section>
</template>
