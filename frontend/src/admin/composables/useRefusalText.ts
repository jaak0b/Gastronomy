import { computed, type ComputedRef, type Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { adminErrorMessageText, type AdminErrorMessage } from '../core/adminErrorMessage'

export function useRefusalText(
  message: Ref<AdminErrorMessage | null>,
): ComputedRef<string | null> {
  const { t } = useI18n()
  return computed(() => (message.value === null ? null : adminErrorMessageText(t, message.value)))
}
