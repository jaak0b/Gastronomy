import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import { useConnectionStore } from '../connection'

export interface AdminPerson {
  serverPersonId: string
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
  serverPerson: { id: string; name: string } | null
}

export const useAdminPeopleStore = defineStore('adminPeople', () => {
  const people = ref<AdminPerson[]>([])
  const loadFailed = ref(false)
  const invitation = ref<Invitation | null>(null)
  const errorMessage = ref<AdminErrorMessage | null>(null)
  const enrolledName = ref<string | null>(null)

  async function load(): Promise<void> {
    loadFailed.value = false
    const result = await request<unknown>('/api/admin/server-people')
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    const rows = listFrom<AdminPerson>(result.data, 'people')
    if (rows === null) {
      loadFailed.value = true
      return
    }
    people.value = rows
  }

  async function rename(id: string, name: string): Promise<boolean> {
    return await commit(`/api/admin/server-people/${id}`, 'PUT', { name })
  }

  async function revokeDevice(id: string): Promise<boolean> {
    return await commit(`/api/admin/server-people/${id}/revoke-device`, 'POST', undefined)
  }

  async function setActive(id: string, isActive: boolean): Promise<boolean> {
    const action = isActive ? 'activate' : 'deactivate'
    return await commit(`/api/admin/server-people/${id}/${action}`, 'POST', undefined)
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

  async function createInvitation(serverPersonId?: string): Promise<void> {
    enrolledName.value = null
    errorMessage.value = null
    const result = await request<Invitation>('/api/admin/enrolment/invitations', {
      method: 'POST',
      body: serverPersonId === undefined ? {} : { serverPersonId },
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
    connection.onEvent<{ serverPersonName: string }>('EnrolmentCompleted', (payload) => {
      enrolledName.value = payload.serverPersonName
      invitation.value = null
      void load()
    })
  }

  return {
    people,
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
