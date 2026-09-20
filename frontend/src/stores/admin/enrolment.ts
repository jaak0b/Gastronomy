import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../../api/client'
import { fetchInvitationQr } from '../../api/invitationQr'
import { adminErrorMessage } from '../../core/adminErrorMessage'
import { adminFailed, adminOk, type AdminActionResult } from '../../core/adminActionResult'
import type { DeviceKind, Invitation } from '../../core/apiTypes'
import type { InvitationQr } from '../../core/invitationQr'
import { assertNever } from '../../core/assertNever'
import { createLatestRequestGate } from '../../core/latestRequestGate'
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
  const enrolled = ref<EnrolledDevice | null>(null)

  const invitationGate = createLatestRequestGate()

  function enrolledNameFor(wanted: DeviceKind): string | null {
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

  const enrolledStaffMemberName = computed(() => enrolledNameFor('staffMember'))
  const enrolledStationName = computed(() => enrolledNameFor('station'))

  async function createInvitation(owner: InvitationOwner): Promise<AdminActionResult<null>> {
    const token = invitationGate.start()
    enrolled.value = null
    invitationQr.value = { kind: 'loading' }
    const result = await request<Invitation>('/api/admin/enrolment/invitations', {
      method: 'POST',
      body: bodyFor(owner),
    })
    if (!invitationGate.isCurrent(token)) {
      return adminOk(null)
    }
    if (result.kind !== 'ok') {
      closeInvitation()
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    invitation.value = result.data
    const qr = await fetchInvitationQr(result.data.invitationId)
    if (!invitationGate.isCurrent(token)) {
      return adminOk(null)
    }
    invitationQr.value = qr
    return adminOk(null)
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
    enrolled,
    enrolledStaffMemberName,
    enrolledStationName,
    createInvitation,
    closeInvitation,
    dismissEnrolled,
    listen,
  }
})
