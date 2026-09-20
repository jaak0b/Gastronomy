import { onMounted, onUnmounted, ref } from 'vue'
import { theFingerLeftTheSpotItPressed, theTapReachesTheButton } from '../core/dockedStripTaps'

export interface DockedStripTapGuard {
  beginPressTracking: (event: PointerEvent) => void
  trackPressMovement: (event: PointerEvent) => void
  suppressTapIfItWasAScrollRelease: (event: MouseEvent) => void
}

export function useDockedStripTapGuard(): DockedStripTapGuard {
  const lastScrollAtMillisecond = ref<number | null>(null)
  const spotThePressStartedOn = ref<{ x: number; y: number } | null>(null)
  const theFingerLeft = ref(false)

  function recordScrollTimestamp(): void {
    lastScrollAtMillisecond.value = Date.now()
  }

  onMounted(() => {
    window.addEventListener('scroll', recordScrollTimestamp, { capture: true, passive: true })
  })

  onUnmounted(() => {
    window.removeEventListener('scroll', recordScrollTimestamp, { capture: true })
  })

  function beginPressTracking(event: PointerEvent): void {
    spotThePressStartedOn.value = { x: event.clientX, y: event.clientY }
    theFingerLeft.value = false
  }

  function trackPressMovement(event: PointerEvent): void {
    const spot = spotThePressStartedOn.value
    if (spot === null) {
      return
    }
    const travel = { fromX: spot.x, fromY: spot.y, toX: event.clientX, toY: event.clientY }
    theFingerLeft.value = theFingerLeft.value || theFingerLeftTheSpotItPressed(travel)
  }

  function suppressTapIfItWasAScrollRelease(event: MouseEvent): void {
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

  return { beginPressTracking, trackPressMovement, suppressTapIfItWasAScrollRelease }
}
