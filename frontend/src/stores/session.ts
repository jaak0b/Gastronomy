import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { onUnauthorisedAnswer, request } from '../api/client'
import type {
  AppLanguage,
  DeviceKind,
  RedeemResponse,
  StaffMember,
  SessionInfo,
  StationIdentity,
} from '../core/apiTypes'
import { assertNever } from '../core/assertNever'
import { restoreDraft } from '../core/draftCart'
import type { DeviceSession } from '../core/landing'
import type { StartingUpFailure } from '../core/startingUp'
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
  const station = ref<StationIdentity | null>(null)
  const language = ref<AppLanguage>(initialLanguage())
  const redeemErrorKey = ref<string | null>(null)
  const isEnrolled = computed(() => deviceToken.value !== null)
  const deviceKind = ref<DeviceKind | null>(null)
  const deviceId = ref<string | null>(null)
  const startingUpFailure = ref<StartingUpFailure | null>(null)
  const deviceSession = computed<DeviceSession>(() => {
    if (deviceToken.value === null) {
      return { state: 'notSetUp' }
    }
    if (deviceKind.value === null) {
      return { state: 'startingUp' }
    }
    return { state: 'setUp', deviceKind: deviceKind.value }
  })

  function heldDraftExists(): boolean {
    return restoreDraft().draft.lines.length > 0
  }

  function storeToken(token: string, kind: DeviceKind, id: string): void {
    deviceToken.value = token
    deviceKind.value = kind
    deviceId.value = id
    startingUpFailure.value = null
    localStorage.setItem(TOKEN_STORAGE_KEY, token)
  }

  function clearToken(): void {
    const tokenThisTabHeld = deviceToken.value
    deviceToken.value = null
    deviceKind.value = null
    deviceId.value = null
    startingUpFailure.value = null
    staffMember.value = null
    station.value = null
    if (localStorage.getItem(TOKEN_STORAGE_KEY) === tokenThisTabHeld) {
      localStorage.removeItem(TOKEN_STORAGE_KEY)
    }
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
        previousDeviceToken: localStorage.getItem(TOKEN_STORAGE_KEY),
      },
    })
    switch (result.kind) {
      case 'ok':
        storeToken(result.data.deviceToken, result.data.deviceKind, result.data.deviceId)
        staffMember.value = result.data.staffMember
        station.value = result.data.station ?? null
        language.value = result.data.language
        storeLanguage(result.data.language)
        return true
      case 'unreachable':
        redeemErrorKey.value = 'enrol.error.noConnection'
        return false
      case 'error':
        redeemErrorKey.value = errorKeyFor(result.status, result.body?.messageKey ?? null)
        return false
      default:
        return assertNever(result)
    }
  }

  async function loadSession(): Promise<void> {
    const tokenTheAnswerBelongsTo = deviceToken.value
    if (tokenTheAnswerBelongsTo === null) {
      return
    }
    startingUpFailure.value = null
    const result = await request<SessionInfo>('/api/session', { token: tokenTheAnswerBelongsTo })
    if (deviceToken.value !== tokenTheAnswerBelongsTo) {
      return
    }
    switch (result.kind) {
      case 'ok':
        staffMember.value = result.data.staffMember
        station.value = result.data.station
        deviceKind.value = result.data.deviceKind
        deviceId.value = result.data.deviceId
        language.value = result.data.language
        return
      case 'error':
        startingUpFailure.value = 'theLaptopCouldNotAnswer'
        return
      case 'unreachable':
        startingUpFailure.value = 'theLaptopWasNotReached'
        return
      default:
        return assertNever(result)
    }
  }

  function watchForBeingSignedOut(): void {
    const connection = useConnectionStore()
    function theDeviceIsNoLongerSetUp(): void {
      clearToken()
    }
    function signOutWhenItIsThisDevice(event: { deviceId: string }): void {
      if (deviceId.value === null) {
        void loadSession()
        return
      }
      if (event.deviceId === deviceId.value) {
        clearToken()
      }
    }
    connection.onEvent<{ deviceId: string }>('DeviceRevoked', signOutWhenItIsThisDevice)
    onUnauthorisedAnswer(theDeviceIsNoLongerSetUp)
  }

  return {
    deviceToken,
    deviceKind,
    deviceId,
    deviceSession,
    startingUpFailure,
    staffMember,
    station,
    language,
    redeemErrorKey,
    isEnrolled,
    heldDraftExists,
    redeem,
    loadSession,
    setLanguage,
    clearToken,
    watchForBeingSignedOut,
  }
})
