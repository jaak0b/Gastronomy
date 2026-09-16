import { onMounted, onUnmounted, ref, type Ref } from 'vue'
import { keyboardInsetPx } from '../core/keyboardInset'

const inset = ref(0)
let consumers = 0

function measure(): void {
  const view = window.visualViewport
  if (!view || view.scale !== 1) {
    inset.value = 0
    return
  }
  inset.value = keyboardInsetPx(window.innerHeight, view.height)
}

export function useKeyboardInset(): Ref<number> {
  onMounted(() => {
    if (consumers === 0) {
      measure()
      window.visualViewport?.addEventListener('resize', measure)
    }
    consumers += 1
  })

  onUnmounted(() => {
    consumers -= 1
    if (consumers === 0) {
      window.visualViewport?.removeEventListener('resize', measure)
    }
  })

  return inset
}
