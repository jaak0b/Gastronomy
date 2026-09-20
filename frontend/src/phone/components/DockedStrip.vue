<script setup lang="ts">
import { useDockedStripTapGuard } from '../composables/useDockedStripTapGuard'

const { beginPressTracking, trackPressMovement, suppressTapIfItWasAScrollRelease } =
  useDockedStripTapGuard()
</script>

<template>
  <v-sheet
    class="docked-strip"
    color="background"
    @pointerdown="beginPressTracking"
    @pointermove="trackPressMovement"
    @click.capture="suppressTapIfItWasAScrollRelease"
  >
    <slot />
  </v-sheet>
</template>

<style scoped>
.docked-strip {
  position: sticky;
  bottom: 0;
  z-index: 2;
  border-top: thin solid rgba(var(--v-border-color), var(--v-border-opacity));
}
</style>
