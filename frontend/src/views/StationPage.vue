<script setup lang="ts">
import { computed, onMounted, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useStationStore, TAKE_DELAY_SECONDS } from '../stores/station'
import { useConnectionStore } from '../stores/connection'
import { currentRoute } from '../router'
import StationWarning from '../components/station/StationWarning.vue'
import StationFilter from '../components/station/StationFilter.vue'
import StationTicketRow from '../components/station/StationTicketRow.vue'
import StationLanguageSwitch from '../components/station/StationLanguageSwitch.vue'

const { t, locale } = useI18n()
const station = useStationStore()
const connection = useConnectionStore()

const accessKey = computed(() => {
  const route = currentRoute.value
  return route.name === 'station' ? route.accessKey : ''
})

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

watch(
  () => station.language,
  (next) => {
    locale.value = next
  },
  { immediate: true },
)

onMounted(async () => {
  await station.open(accessKey.value)
  connection.onEvent('OrderAccepted', () => {
    void station.loadTickets()
  })
  connection.onEvent('TicketStatusChanged', () => {
    void station.loadTickets()
  })
  connection.onEvent('PrinterStatusChanged', () => {
    void station.loadTickets()
    void station.loadPrinter()
  })
  await connection.connect(accessKey.value)
})
</script>

<template>
  <p v-if="station.keyIsUnknown" class="unknown-key">{{ t('station.unknownKey') }}</p>
  <section v-else class="station-page">
    <h1>{{ t('station.title', { name: selectedName }) }}</h1>
    <StationLanguageSwitch :language="station.language" @select="station.setLanguage" />
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
