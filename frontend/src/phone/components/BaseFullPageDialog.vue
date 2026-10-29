<script setup lang="ts">
import { useKeyboardInset } from '../composables/useKeyboardInset'

defineProps<{ title: string }>()

const keyboardInset = useKeyboardInset()
</script>

<template>
  <v-dialog model-value persistent fullscreen>
    <v-card class="card d-flex flex-column" data-test="card" :style="{ paddingBottom: `${keyboardInset}px` }">
      <v-card-title class="title" data-test="page-title">{{ title }}</v-card-title>
      <v-card-text class="body flex-grow-1">
        <slot />
      </v-card-text>
      <v-card-actions class="actions flex-column align-stretch" data-test="actions">
        <slot name="actions" />
      </v-card-actions>
    </v-card>
  </v-dialog>
</template>

<style scoped>
.card {
  height: 100%;
  border-radius: 0;
}

.body {
  font-size: var(--app-text-size);
  line-height: 1.4;
}

.title {
  white-space: normal;
  overflow: visible;
  text-overflow: clip;
  line-height: 1.35;
  padding-block: 1rem;
}

.actions {
  padding: 0 1rem 1rem;
}

.actions :deep(.v-btn) {
  width: 100%;
  min-width: 0;
  height: auto;
  min-height: 3.5rem;
  padding-block: 0.75rem;
  text-transform: none;
  letter-spacing: normal;
  margin-inline-start: 0;
}

.actions :deep(.v-btn .v-btn__content) {
  white-space: normal;
}
</style>
