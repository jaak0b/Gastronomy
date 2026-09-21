import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request } from '../../shared/api/client'
import { fetchInvitationQr } from '../api/invitationQr'
import { DeviceOwnerKind, InvitationView } from '../../shared/api/generatedSchemas'
import { adminOk, type AdminActionResult } from '../core/adminActionResult'
import { adminFailureFrom } from '../core/adminMutation'
import type { InvitationQr } from '../core/invitationQr'
import { assertNever } from '../../shared/core/assertNever'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'

export type InvitationOwner =
  | { kind: 'somebodyNew' }
  | { kind: 'staffMember'; staffMemberId: string }
  | { kind: 'station'; stationId: string }

export interface EnrolledDevice {
  deviceKind: DeviceOwnerKind
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
  const invitation = ref<InvitationView | null>(null)
  const invitationQr = ref<InvitationQr>({ kind: 'loading' })
  const enrolled = ref<EnrolledDevice | null>(null)

  const invitationGate = createLatestRequestGate()

  function enrolledNameFor(wanted: DeviceOwnerKind): string | null {
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
    const token = invitationGate.startRequest()
    enrolled.value = null
    invitationQr.value = { kind: 'loading' }
    const result = await request('/api/admin/enrolment/invitations', {
      method: 'POST',
      body: bodyFor(owner),
      schema: InvitationView,
    })
    if (!invitationGate.isNewestRequest(token)) {
      return adminOk(null)
    }
    if (result.kind !== 'ok') {
      closeInvitation()
      return adminFailureFrom(result)
    }
    invitation.value = result.data
    const qr = await fetchInvitationQr(result.data.invitationId)
    if (!invitationGate.isNewestRequest(token)) {
      return adminOk(null)
    }
    invitationQr.value = qr
    return adminOk(null)
  }

  function closeInvitation(): void {
    invitation.value = null
    invitationQr.value = { kind: 'loading' }
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
    listen,
  }
})
