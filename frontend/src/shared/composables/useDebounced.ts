import { onUnmounted } from 'vue'

export function useDebounced<T>(call: (value: T) => void, delayMs: number) {
  let pendingTimer: ReturnType<typeof setTimeout> | null = null

  function cancel(): void {
    if (pendingTimer !== null) {
      clearTimeout(pendingTimer)
      pendingTimer = null
    }
  }

  function callAfterTheDelay(value: T): void {
    cancel()
    pendingTimer = setTimeout(() => {
      pendingTimer = null
      call(value)
    }, delayMs)
  }

  onUnmounted(cancel)

  return { callAfterTheDelay, cancel }
}
