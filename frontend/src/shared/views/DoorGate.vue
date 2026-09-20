<script setup lang="ts">
import { useI18n } from 'vue-i18n'
import { buildBackButtonTrap } from '../router/backButtonTrap'

const { t } = useI18n()
const theIcon = '/favicon.svg'
</script>

<template>
  <v-container class="door-gate" fluid>
    <div class="door-gate-panel text-center">
      <img class="door-gate-icon" :src="theIcon" alt="" />
      <p class="door-gate-hint text-body-1 mb-6">{{ t('doorGate.hint') }}</p>
      <v-btn class="door-gate-open" color="primary" size="x-large" block @click="buildBackButtonTrap">
        {{ t('doorGate.open') }}
      </v-btn>
    </div>
  </v-container>
</template>

<style scoped>
.door-gate {
  min-height: 100dvh;
  display: grid;
  place-items: center;
}

.door-gate-panel {
  width: min(22rem, 100%);
}

.door-gate-icon {
  width: 6rem;
  height: 6rem;
  margin-bottom: 1.5rem;
}
</style>
