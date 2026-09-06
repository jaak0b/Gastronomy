<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import { useI18n } from 'vue-i18n'
import type { ProductionAdvance } from '../core/stationBoard'
import { useStationStore } from '../stores/station'
import { useSessionStore } from '../stores/session'
import LanguageSwitch from '../components/LanguageSwitch.vue'
import StationSliceCard from '../components/station/StationSliceCard.vue'
import StationItemCard from '../components/station/StationItemCard.vue'

const { t } = useI18n()
const station = useStationStore()
const session = useSessionStore()
let stopListening: (() => void) | null = null

const stationName = computed(() => station.station?.name ?? session.station?.name ?? '')

onMounted(async () => {
  stopListening = station.listen()
  await station.load()
})

onUnmounted(() => {
  stopListening?.()
  stopListening = null
})

async function advance(orderItemIds: string[], status: ProductionAdvance): Promise<void> {
  await station.advance(orderItemIds, status)
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

    <v-alert
      v-if="station.readyTableName !== null"
      class="ready-notice mb-4"
      type="success"
      variant="flat"
      prominent
    >
      <span class="ready-text text-h5">
        {{ t('station.finishedNotice', { table: station.readyTableName }) }}
      </span>
      <template #append>
        <v-btn class="dismiss" variant="text" size="large" @click="station.dismissReadyNotice">
          {{ t('station.dismiss') }}
        </v-btn>
      </template>
    </v-alert>

    <v-alert v-if="station.loadFailed" class="load-failed mb-4" type="warning" variant="tonal">
      {{ t('station.loadFailed') }}
    </v-alert>
    <v-alert
      v-if="station.failureKey !== null"
      class="action-failed mb-4"
      type="warning"
      variant="tonal"
    >
      {{ t(station.failureKey) }}
    </v-alert>
    <v-alert v-if="station.hasNothingToPrepare" class="empty mb-4" type="info" variant="tonal">
      {{ t('station.empty') }}
    </v-alert>

    <v-row>
      <v-col cols="12" md="6" class="together-column">
        <h2 class="together-heading text-h6 mb-2">{{ t('station.togetherHeading') }}</h2>
        <StationSliceCard
          v-for="slice in station.board.together"
          :key="slice.stationOrderId"
          :slice="slice"
          :is-working="station.isWorking"
          @advance="advance"
        />
      </v-col>
      <v-col cols="12" md="6" class="single-column">
        <h2 class="single-heading text-h6 mb-2">{{ t('station.singleHeading') }}</h2>
        <StationItemCard
          v-for="card in station.board.single"
          :key="card.item.orderItemId"
          :card="card"
          :is-working="station.isWorking"
          @advance="advance"
        />
      </v-col>
    </v-row>
  </v-container>
</template>
