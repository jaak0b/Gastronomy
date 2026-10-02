import { ref, type ComputedRef, type Ref } from 'vue'
import { refusalFrom, type AdminActionResult } from '../core/adminActionResult'
import type { AdminErrorMessage } from '../core/adminErrorMessage'
import { useRefusalText } from './useRefusalText'

export function useRefusalDisplay(): {
  refusal: Ref<AdminErrorMessage | null>
  refusalText: ComputedRef<string | null>
  showRefusalOf: (result: AdminActionResult<unknown>) => boolean
} {
  const refusal = ref<AdminErrorMessage | null>(null)
  const refusalText = useRefusalText(refusal)

  function showRefusalOf(result: AdminActionResult<unknown>): boolean {
    refusal.value = refusalFrom(result)
    return refusal.value !== null
  }

  return { refusal, refusalText, showRefusalOf }
}
