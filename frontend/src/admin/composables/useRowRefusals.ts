import { ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { refusalFrom, type AdminActionResult } from '../core/adminActionResult'
import {
  adminErrorMessageForKey,
  adminErrorMessageText,
  type AdminErrorMessage,
} from '../core/adminErrorMessage'

export interface RowRefusals {
  refusalTextOf: (rowId: string) => string | null
  isRefused: (rowId: string) => boolean
  refuse: (rowId: string, messageKey: string, parameters?: Record<string, string | number>) => void
  showRefusalOf: (rowId: string, result: AdminActionResult<unknown>) => void
  forgetRefusalOf: (rowId: string) => void
}

export function useRowRefusals(): RowRefusals {
  const { t } = useI18n()
  const refusals = ref(new Map<string, AdminErrorMessage>())

  function refusalTextOf(rowId: string): string | null {
    return adminErrorMessageText(t, refusals.value.get(rowId) ?? null)
  }

  function isRefused(rowId: string): boolean {
    return refusals.value.has(rowId)
  }

  function refuse(
    rowId: string,
    messageKey: string,
    parameters: Record<string, string | number> = {},
  ): void {
    refusals.value.set(rowId, adminErrorMessageForKey(messageKey, parameters))
  }

  function showRefusalOf(rowId: string, result: AdminActionResult<unknown>): void {
    const refusal = refusalFrom(result)
    if (refusal !== null) {
      refusals.value.set(rowId, refusal)
    }
  }

  function forgetRefusalOf(rowId: string): void {
    refusals.value.delete(rowId)
  }

  return { refusalTextOf, isRefused, refuse, showRefusalOf, forgetRefusalOf }
}
