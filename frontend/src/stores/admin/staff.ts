import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { fetchInvitationQr } from '../../api/invitationQr'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import type { InvitationQr } from '../../core/invitationQr'
import { useConnectionStore } from '../connection'

export interface AdminStaffMember {
  staffMemberId: string
  name: string
  isActive: boolean
  hasDevice: boolean
  lastSeenAtUtc: string | null
  userAgent: string | null
  hasOutstandingInvitation: boolean
}

export interface Invitation {
  invitationId: string
  qrUrl: string
  expiresAtUtc: string
  staffMember: { id: string; name: string } | null
}

export const useAdminStaffStore = defineStore('adminStaff', () => {
  const staffMembers = ref<AdminStaffMember[]>([])
  const loadFailed = ref(false)
  const invitation = ref<Invitation | null>(null)
  const invitationQr = ref<InvitationQr>({ kind: 'loading' })
  const errorMessage = ref<AdminErrorMessage | null>(null)
  const enrolledName = ref<string | null>(null)

  async function load(): Promise<void> {
    loadFailed.value = false
    const result = await request<unknown>('/api/admin/staff-members')
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    const rows = listFrom<AdminStaffMember>(result.data, 'staffMembers')
    if (rows === null) {
      loadFailed.value = true
      return
    }
    staffMembers.value = rows
  }

  async function rename(id: string, name: string): Promise<boolean> {
    return await commit(`/api/admin/staff-members/${id}`, 'PUT', { name })
  }

  async function setActive(id: string, isActive: boolean): Promise<boolean> {
    const action = isActive ? 'activate' : 'deactivate'
    return await commit(`/api/admin/staff-members/${id}/${action}`, 'POST', undefined)
  }

  async function commit(path: string, method: 'POST' | 'PUT', body: unknown): Promise<boolean> {
    errorMessage.value = null
    const result = await request(path, { method, body })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return false
    }
    await load()
    return true
  }

  async function createInvitation(staffMemberId?: string): Promise<void> {
    enrolledName.value = null
    errorMessage.value = null
    invitationQr.value = { kind: 'loading' }
    const result = await request<Invitation>('/api/admin/enrolment/invitations', {
      method: 'POST',
      body: staffMemberId === undefined ? {} : { staffMemberId },
    })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    invitation.value = result.data
    invitationQr.value = await fetchInvitationQr(result.data.invitationId)
  }

  function closeInvitation(): void {
    invitation.value = null
    invitationQr.value = { kind: 'loading' }
  }

  function listen(): void {
    const connection = useConnectionStore()
    connection.registerRefetch(load)
    connection.onEvent<{ staffMemberName: string }>('EnrolmentCompleted', (payload) => {
      enrolledName.value = payload.staffMemberName
      closeInvitation()
      void load()
    })
  }

  return {
    staffMembers,
    loadFailed,
    errorMessage,
    invitation,
    invitationQr,
    enrolledName,
    load,
    rename,
    setActive,
    createInvitation,
    closeInvitation,
    listen,
  }
})
