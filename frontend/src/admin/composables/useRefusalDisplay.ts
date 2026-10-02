import { computed, ref, type ComputedRef, type Ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { refusalFrom, type AdminActionResult } from '../core/adminActionResult'
import { adminErrorMessageText, type AdminErrorMessage } from '../core/adminErrorMessage'

export function useRefusalDisplay(): {
  refusal: Ref<AdminErrorMessage | null>
  refusalText: ComputedRef<string | null>
  showRefusalOf: (result: AdminActionResult<unknown>) => boolean
} {
  const { t } = useI18n()
  const refusal = ref<AdminErrorMessage | null>(null)
  const refusalText = computed(() =>
    refusal.value === null ? null : adminErrorMessageText(t, refusal.value),
  )

  function showRefusalOf(result: AdminActionResult<unknown>): boolean {
    refusal.value = refusalFrom(result)
    return refusal.value !== null
  }

  return { refusal, refusalText, showRefusalOf }
}
