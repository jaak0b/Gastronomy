import { computed, ref, watch, type ComputedRef } from 'vue'
import { useI18n } from 'vue-i18n'
import type { AdminErrorMessage } from '../../core/adminErrorMessage'

type RefusalSource = () => AdminErrorMessage | null | undefined

function anyStillStanding(sources: RefusalSource[]): AdminErrorMessage | null {
  return sources.map((source) => source() ?? null).find((message) => message !== null) ?? null
}

export function useRefusalText(sources: RefusalSource[]): ComputedRef<string | null> {
  const { t } = useI18n()
  const newest = ref<AdminErrorMessage | null>(anyStillStanding(sources))

  for (const source of sources) {
    watch(source, (message) => {
      newest.value = message ?? anyStillStanding(sources)
    })
  }

  return computed(() => {
    const message = newest.value
    if (message === null) {
      return null
    }
    return message.count === null
      ? t(message.key, message.parameters)
      : t(message.key, message.parameters, message.count)
  })
}
