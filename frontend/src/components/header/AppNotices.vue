<script setup lang="ts">
import { computed } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../core/assertNever'
import { useConnectionStore } from '../../stores/connection'
import { useOrderStore } from '../../stores/order'
import { usePrinterStatusStore } from '../../stores/printerStatus'

const { t } = useI18n()
const connection = useConnectionStore()
const printerStatus = usePrinterStatusStore()
const order = useOrderStore()

const hasArrived = computed(
  () => order.sendState === 'accepted' && order.acceptedOrderNumber !== null,
)

const connectionKey = computed<string | null>(() => {
  const state = connection.state
  switch (state) {
    case 'connected':
      return connection.recovered ? 'header.backOnline' : null
    case 'reconnecting':
    case 'offline':
      return 'header.reconnecting'
    default:
      return assertNever(state)
  }
})

const banners = computed(() =>
  printerStatus.banners.map((banner) => ({
    stationId: banner.stationId,
    text:
      banner.waitingCount === null
        ? t(banner.key, { name: banner.name })
        : `${t(banner.key, { name: banner.name })} ${t('header.stationWaiting', { count: banner.waitingCount }, banner.waitingCount)}`,
  })),
)
</script>

<template>
  <v-alert
    v-if="hasArrived"
    class="sent"
    type="success"
    variant="tonal"
    rounded="0"
    density="compact"
    @click="order.dismissConfirmation"
  >
    {{ t('review.sent', { number: order.acceptedOrderNumber }) }}
  </v-alert>
  <v-alert
    v-if="order.draftWasLost"
    class="draft-lost"
    type="warning"
    variant="tonal"
    rounded="0"
    density="compact"
    closable
    @click:close="order.dismissDraftLoss"
  >
    {{ t('order.draftLost') }}
  </v-alert>
  <v-alert
    v-if="connectionKey !== null"
    class="connection"
    type="info"
    variant="tonal"
    rounded="0"
    density="compact"
  >
    {{ t(connectionKey) }}
  </v-alert>
  <v-alert
    v-for="banner in banners"
    :key="banner.stationId"
    class="station-banner"
    type="warning"
    variant="tonal"
    rounded="0"
    density="compact"
  >
    {{ banner.text }}
  </v-alert>
</template>
