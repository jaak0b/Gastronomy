import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../api/client'
import type { AppLanguage, RedeemResponse, StaffMember, SessionInfo } from '../core/apiTypes'
import { restoreDraft } from '../core/draftCart'
import { useConnectionStore } from './connection'
import { LANGUAGE_STORAGE_KEY, initialLanguage, storeLanguage } from '../appLanguage'

export const TOKEN_STORAGE_KEY = 'deviceToken'
export { LANGUAGE_STORAGE_KEY }

export interface RedeemInput {
  code?: string
  name?: string
}

export const useSessionStore = defineStore('session', () => {
  const deviceToken = ref<string | null>(localStorage.getItem(TOKEN_STORAGE_KEY))
  const staffMember = ref<StaffMember | null>(null)
  const language = ref<AppLanguage>(initialLanguage())
  const redeemErrorKey = ref<string | null>(null)
  const isEnrolled = computed(() => deviceToken.value !== null)

  function heldDraftExists(): boolean {
    return restoreDraft().draft.lines.length > 0
  }

  function storeToken(token: string): void {
    deviceToken.value = token
    localStorage.setItem(TOKEN_STORAGE_KEY, token)
  }

  function clearToken(): void {
    deviceToken.value = null
    staffMember.value = null
    localStorage.removeItem(TOKEN_STORAGE_KEY)
  }

  function setLanguage(next: AppLanguage): void {
    language.value = next
    storeLanguage(next)
    if (deviceToken.value === null) {
      return
    }
    void request('/api/session/language', {
      method: 'PUT',
      body: { language: next },
      token: deviceToken.value,
    })
  }

  function errorKeyFor(status: number, messageKey: string | null): string {
    if (messageKey !== null) {
      return messageKey
    }
    switch (status) {
      case 404:
        return 'enrolCode.error.wrong'
      case 429:
        return 'enrolCode.error.retired'
      default:
        return 'enrol.error.codeUsed'
    }
  }

  async function redeem(input: RedeemInput): Promise<boolean> {
    redeemErrorKey.value = null
    const result = await request<RedeemResponse>('/api/enrolment/redeem', {
      method: 'POST',
      body: {
        code: input.code ?? null,
        name: input.name ?? null,
        userAgent: navigator.userAgent,
      },
    })
    switch (result.kind) {
      case 'ok':
        storeToken(result.data.deviceToken)
        staffMember.value = result.data.staffMember
        language.value = result.data.language
        storeLanguage(result.data.language)
        return true
      case 'unreachable':
        redeemErrorKey.value = 'enrol.error.noConnection'
        return false
      case 'error':
        redeemErrorKey.value = errorKeyFor(result.status, result.body?.messageKey ?? null)
        return false
    }
  }

  async function loadSession(): Promise<void> {
    if (deviceToken.value === null) {
      return
    }
    const result = await request<SessionInfo>('/api/session', { token: deviceToken.value })
    switch (result.kind) {
      case 'ok':
        staffMember.value = result.data.staffMember
        language.value = result.data.language
        return
      case 'error':
        if (result.status === 401) {
          clearToken()
        }
        return
      case 'unreachable':
        return
    }
  }

  function listenForRevocation(): void {
    const connection = useConnectionStore()
    connection.onEvent<{ deviceId: string }>('DeviceRevoked', () => {
      clearToken()
    })
  }

  return {
    deviceToken,
    staffMember,
    language,
    redeemErrorKey,
    isEnrolled,
    heldDraftExists,
    redeem,
    loadSession,
    setLanguage,
    clearToken,
    listenForRevocation,
  }
})
