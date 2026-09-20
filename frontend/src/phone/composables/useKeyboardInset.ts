import { onMounted, onUnmounted, ref, type Ref } from 'vue'
import { keyboardInsetPx } from '../core/keyboardInset'

export function useKeyboardInset(): Ref<number> {
  const inset = ref(0)

  function measure(): void {
    const view = window.visualViewport
    if (!view || view.scale !== 1) {
      inset.value = 0
      return
    }
    inset.value = keyboardInsetPx(window.innerHeight, view.height)
  }

  onMounted(() => {
    measure()
    window.visualViewport?.addEventListener('resize', measure)
  })

  onUnmounted(() => {
    window.visualViewport?.removeEventListener('resize', measure)
  })

  return inset
}
