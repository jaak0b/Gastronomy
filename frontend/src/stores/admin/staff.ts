import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
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
  sixDigitCode: string
  expiresAtUtc: string
  staffMember: { id: string; name: string } | null
}

export const useAdminStaffStore = defineStore('adminStaff', () => {
  const staffMembers = ref<AdminStaffMember[]>([])
  const loadFailed = ref(false)
  const invitation = ref<Invitation | null>(null)
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

  async function revokeDevice(id: string): Promise<boolean> {
    return await commit(`/api/admin/staff-members/${id}/revoke-device`, 'POST', undefined)
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
    const result = await request<Invitation>('/api/admin/enrolment/invitations', {
      method: 'POST',
      body: staffMemberId === undefined ? {} : { staffMemberId },
    })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    invitation.value = result.data
  }

  function closeInvitation(): void {
    invitation.value = null
  }

  function listen(): void {
    const connection = useConnectionStore()
    connection.registerRefetch(load)
    connection.onEvent<{ staffMemberName: string }>('EnrolmentCompleted', (payload) => {
      enrolledName.value = payload.staffMemberName
      invitation.value = null
      void load()
    })
  }

  return {
    staffMembers,
    loadFailed,
    errorMessage,
    invitation,
    enrolledName,
    load,
    rename,
    revokeDevice,
    setActive,
    createInvitation,
    closeInvitation,
    listen,
  }
})
