import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../../api/client'
import { fetchInvitationQr } from '../../api/invitationQr'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import type { DeviceKind, Invitation } from '../../core/apiTypes'
import type { InvitationQr } from '../../core/invitationQr'
import { assertNever } from '../../core/assertNever'
import { useConnectionStore } from '../connection'


export type InvitationOwner =
  | { kind: 'somebodyNew' }
  | { kind: 'staffMember'; staffMemberId: string }
  | { kind: 'station'; stationId: string }

export interface EnrolledDevice {
  deviceKind: DeviceKind
  ownerName: string
}

function bodyFor(owner: InvitationOwner): Record<string, string> {
  switch (owner.kind) {
    case 'somebodyNew':
      return {}
    case 'staffMember':
      return { staffMemberId: owner.staffMemberId }
    case 'station':
      return { stationId: owner.stationId }
    default:
      return assertNever(owner)
  }
}

export const useAdminEnrolmentStore = defineStore('adminEnrolment', () => {
  const invitation = ref<Invitation | null>(null)
  const invitationQr = ref<InvitationQr>({ kind: 'loading' })
  const errorMessage = ref<AdminErrorMessage | null>(null)
  const enrolled = ref<EnrolledDevice | null>(null)

  function enrolledNameOf(wanted: DeviceKind): string | null {
    const device = enrolled.value
    if (device === null) {
      return null
    }
    switch (device.deviceKind) {
      case 'staffMember':
        return wanted === 'staffMember' ? device.ownerName : null
      case 'station':
        return wanted === 'station' ? device.ownerName : null
      default:
        return assertNever(device.deviceKind)
    }
  }

  const enrolledStaffMemberName = computed(() => enrolledNameOf('staffMember'))
  const enrolledStationName = computed(() => enrolledNameOf('station'))

  async function createInvitation(owner: InvitationOwner): Promise<void> {
    enrolled.value = null
    errorMessage.value = null
    invitationQr.value = { kind: 'loading' }
    const result = await request<Invitation>('/api/admin/enrolment/invitations', {
      method: 'POST',
      body: bodyFor(owner),
    })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      closeInvitation()
      return
    }
    invitation.value = result.data
    invitationQr.value = await fetchInvitationQr(result.data.invitationId)
  }

  function closeInvitation(): void {
    invitation.value = null
    invitationQr.value = { kind: 'loading' }
  }

  function dismissEnrolled(): void {
    enrolled.value = null
  }

  function listen(onEnrolled: () => void): () => void {
    const connection = useConnectionStore()
    return connection.onEvent<EnrolledDevice>('EnrolmentCompleted', (payload) => {
      enrolled.value = payload
      closeInvitation()
      onEnrolled()
    })
  }

  return {
    invitation,
    invitationQr,
    errorMessage,
    enrolled,
    enrolledStaffMemberName,
    enrolledStationName,
    createInvitation,
    closeInvitation,
    dismissEnrolled,
    listen,
  }
})
