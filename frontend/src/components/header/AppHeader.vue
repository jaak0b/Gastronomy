<script setup lang="ts">
import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useOrderStore } from '../../stores/order'
import { navigate } from '../../router'
import SettingsSheet from './SettingsSheet.vue'

const { t } = useI18n()
const order = useOrderStore()
const settingsAreOpen = ref(false)
</script>

<template>
  <v-app-bar class="app-header" density="comfortable" color="primary">
    <v-btn class="catalog-link" variant="text" @click="navigate('/')">
      {{ t('catalog.title') }}
    </v-btn>
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
    <v-btn
      class="settings"
      icon="mdi-cog"
      variant="text"
      :aria-label="t('header.settings')"
      @click="settingsAreOpen = true"
    >
      <v-icon icon="mdi-cog" />
      <v-tooltip activator="parent" location="bottom">{{ t('header.settings') }}</v-tooltip>
    </v-btn>
  </v-app-bar>
  <SettingsSheet v-if="settingsAreOpen" @close="settingsAreOpen = false" />
</template>
