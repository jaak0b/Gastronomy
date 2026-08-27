<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../core/assertNever'
import { useConnectionStore } from '../../stores/connection'
import { useOrderStore } from '../../stores/order'
import { usePrinterStatusStore } from '../../stores/printerStatus'
import { navigate } from '../../router'
import SettingsSheet from './SettingsSheet.vue'

const { t } = useI18n()
const connection = useConnectionStore()
const order = useOrderStore()
const printerStatus = usePrinterStatusStore()
const settingsAreOpen = ref(false)

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
    key: banner.key,
    text:
      banner.waitingCount === null
        ? t(banner.key, { name: banner.name })
        : `${t(banner.key, { name: banner.name })} ${t('header.stationWaiting', { count: banner.waitingCount }, banner.waitingCount)}`,
  })),
)
</script>

<template>
  <v-app-bar class="app-header" density="comfortable" color="primary">
    <v-btn class="stations-link" variant="text" @click="navigate('/stations')">
      {{ t('header.stations') }}
    </v-btn>
    <v-btn class="orders-link" variant="text" @click="navigate('/orders')">
      <span>{{ t('orders.title') }}</span>
      <v-chip v-if="order.attentionCount > 0" class="attention ms-2" size="small" color="error">
        {{ t('header.attention', { count: order.attentionCount }, order.attentionCount) }}
      </v-chip>
    </v-btn>
    <v-spacer />
    <span v-if="connectionKey !== null" class="connection me-2">{{ t(connectionKey) }}</span>
    <v-btn class="settings" variant="text" @click="settingsAreOpen = true">
      {{ t('header.settings') }}
    </v-btn>
  </v-app-bar>
  <v-alert
    v-for="banner in banners"
    :key="banner.key"
    class="station-banner"
    type="warning"
    variant="tonal"
    rounded="0"
  >
    {{ banner.text }}
  </v-alert>
  <SettingsSheet v-if="settingsAreOpen" @close="settingsAreOpen = false" />
</template>
