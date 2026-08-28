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
  <v-app-bar class="app-header" height="72" color="primary">
    <div class="destinations d-flex flex-grow-1">
      <v-btn class="catalog-link flex-grow-1" variant="text" stacked @click="navigate('/')">
        <v-icon icon="mdi-clipboard-text-outline" />
        <span class="label">{{ t('catalog.title') }}</span>
      </v-btn>
      <v-btn
        class="stations-link flex-grow-1"
        variant="text"
        stacked
        @click="navigate('/stations')"
      >
        <v-icon icon="mdi-store-outline" />
        <span class="label">{{ t('header.stations') }}</span>
      </v-btn>
      <v-btn class="orders-link flex-grow-1" variant="text" stacked @click="navigate('/orders')">
        <v-badge
          v-if="order.attentionCount > 0"
          class="attention"
          color="error"
          :content="order.attentionCount"
          :aria-label="t('header.attention', { count: order.attentionCount }, order.attentionCount)"
        >
          <v-icon icon="mdi-format-list-checks" />
        </v-badge>
        <v-icon v-else icon="mdi-format-list-checks" />
        <span class="label">{{ t('orders.title') }}</span>
      </v-btn>
    </div>
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

<style scoped>
.label {
  font-size: 0.6875rem;
  line-height: 1.1;
  text-align: center;
  white-space: normal;
  text-transform: none;
  letter-spacing: normal;
}

.destinations .v-btn {
  min-width: 0;
  padding-inline: 4px;
}
</style>
