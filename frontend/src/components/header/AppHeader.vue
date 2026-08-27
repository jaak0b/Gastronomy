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
  <header class="app-header">
    <button type="button" class="stations-link" @click="navigate('/stations')">
      {{ t('header.stations') }}
    </button>
    <button type="button" class="orders-link" @click="navigate('/orders')">
      <span>{{ t('orders.title') }}</span>
      <span v-if="order.attentionCount > 0" class="attention">
        {{ t('header.attention', { count: order.attentionCount }, order.attentionCount) }}
      </span>
    </button>
    <span v-if="connectionKey !== null" class="connection">{{ t(connectionKey) }}</span>
    <button type="button" class="settings" @click="settingsAreOpen = true">
      {{ t('header.settings') }}
    </button>
  </header>
  <p v-for="banner in banners" :key="banner.key" class="station-banner">{{ banner.text }}</p>
  <SettingsSheet v-if="settingsAreOpen" @close="settingsAreOpen = false" />
</template>
