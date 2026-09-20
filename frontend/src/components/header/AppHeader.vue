<script setup lang="ts">
import { computed, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { assertNever } from '../../shared/core/assertNever'
import { currentRoute, navigate } from '../../shared/router/router'
import SettingsSheet from './SettingsSheet.vue'

type Destination = 'catalog' | 'openItems' | 'none'

const { t } = useI18n()
const settingsAreOpen = ref(false)

const currentDestination = computed<Destination>(() => {
  const route = currentRoute.value
  switch (route.name) {
    case 'home':
    case 'review':
      return 'catalog'
    case 'openItems':
      return 'openItems'
    case 'enrolQr':
    case 'stations':
    case 'admin':
      return 'none'
    default:
      return assertNever(route)
  }
})
</script>

<template>
  <v-app-bar class="app-header" height="72">
    <div class="destinations d-flex flex-grow-1">
      <v-btn
        class="catalog-link flex-grow-1"
        variant="text"
        stacked
        :class="currentDestination === 'catalog' ? 'text-primary' : 'text-medium-emphasis'"
        @click="navigate('/')"
      >
        <v-icon icon="mdi-clipboard-text-outline" />
        <span class="label">{{ t('catalog.title') }}</span>
      </v-btn>
      <v-btn
        class="open-items-link flex-grow-1"
        variant="text"
        stacked
        :class="currentDestination === 'openItems' ? 'text-primary' : 'text-medium-emphasis'"
        @click="navigate('/open-items')"
      >
        <v-icon icon="mdi-cash-register" />
        <span class="label">{{ t('header.openItems') }}</span>
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
}
</style>
