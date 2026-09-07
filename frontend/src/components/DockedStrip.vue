<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { theFingerLeftTheSpotItPressed, theTapReachesTheButton } from '../core/dockedStripTaps'

const lastScrollAtMillisecond = ref<number | null>(null)
const spotThePressStartedOn = ref<{ x: number; y: number } | null>(null)
const theFingerLeft = ref(false)

function noteThatSomethingScrolled(): void {
  lastScrollAtMillisecond.value = Date.now()
}

onMounted(() => {
  window.addEventListener('scroll', noteThatSomethingScrolled, { capture: true, passive: true })
})

onUnmounted(() => {
  window.removeEventListener('scroll', noteThatSomethingScrolled, { capture: true })
})

function notePressStart(event: PointerEvent): void {
  spotThePressStartedOn.value = { x: event.clientX, y: event.clientY }
  theFingerLeft.value = false
}

function noteThatTheFingerMoved(event: PointerEvent): void {
  const spot = spotThePressStartedOn.value
  if (spot === null) {
    return
  }
  const travel = { fromX: spot.x, fromY: spot.y, toX: event.clientX, toY: event.clientY }
  theFingerLeft.value = theFingerLeft.value || theFingerLeftTheSpotItPressed(travel)
}

function letTheTapThroughOrSwallowIt(event: MouseEvent): void {
  const reachesTheButton = theTapReachesTheButton({
    atMillisecond: Date.now(),
    lastScrollAtMillisecond: lastScrollAtMillisecond.value,
    theFingerLeftTheSpotItPressed: theFingerLeft.value,
  })
  spotThePressStartedOn.value = null
  theFingerLeft.value = false
  if (reachesTheButton) {
    return
  }
  event.stopPropagation()
  event.preventDefault()
}
</script>

<template>
  <v-sheet
    class="docked-strip"
    color="background"
    @pointerdown="notePressStart"
    @pointermove="noteThatTheFingerMoved"
    @click.capture="letTheTapThroughOrSwallowIt"
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
