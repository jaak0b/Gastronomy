import { onUnmounted, ref, toValue, watch, type MaybeRefOrGetter, type Ref } from 'vue'
import { closeStep, openStep } from '../router/router'

export interface BackStepOptions {
  isOpen?: MaybeRefOrGetter<boolean>
  refusesBackWhile?: MaybeRefOrGetter<boolean>
}

let stepsOpened = 0

export function useBackStep(close: () => void, options: BackStepOptions = {}): void {
  stepsOpened += 1
  const name = `dialog-${stepsOpened}`
  const handler = { close, refusesBack: () => toValue(options.refusesBackWhile ?? false) }

  watch(
    () => toValue(options.isOpen ?? true),
    (open) => {
      if (open) {
        openStep({ name, value: name, kind: 'dialog' }, handler)
      } else {
        closeStep(name)
      }
    },
    { immediate: true },
  )

  onUnmounted(() => {
    closeStep(name)
  })
}

export function useBackStepForAMenu(): Ref<boolean> {
  const isOpen = ref(false)
  useBackStep(
    () => {
      isOpen.value = false
    },
    { isOpen },
  )
  return isOpen
}
