import { computed, type ComputedRef, type Ref } from 'vue'
import { useI18n, type ComposerTranslation } from 'vue-i18n'
import type { AdminErrorMessage } from '../../core/adminErrorMessage'

export function refusalMessageText(
  translate: ComposerTranslation,
  message: AdminErrorMessage,
): string {
  return message.count === null
    ? translate(message.key, message.parameters)
    : translate(message.key, message.parameters, message.count)
}

export function useRefusalText(
  message: Ref<AdminErrorMessage | null>,
): ComputedRef<string | null> {
  const { t } = useI18n()
  return computed(() => (message.value === null ? null : refusalMessageText(t, message.value)))
}
