import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request } from '../../api/client'
import { useConnectionStore } from '../connection'

export interface AdminPerson {
  id: string
  name: string
  hasPhone: boolean
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
  const invitation = ref<Invitation | null>(null)
  const enrolledName = ref<string | null>(null)

  async function load(): Promise<void> {
    const result = await request<{ people: AdminPerson[] }>('/api/admin/server-people')
    if (result.kind === 'ok') {
      people.value = result.data.people
    }
  }

  async function rename(id: string, name: string): Promise<void> {
    await request(`/api/admin/server-people/${id}`, { method: 'PUT', body: { name } })
    await load()
  }

  async function revokeDevice(id: string): Promise<void> {
    await request(`/api/admin/server-people/${id}/revoke-device`, { method: 'POST' })
    await load()
  }

  async function deactivate(id: string): Promise<void> {
    await request(`/api/admin/server-people/${id}/deactivate`, { method: 'POST' })
    await load()
  }

  async function createInvitation(serverPersonId?: string): Promise<void> {
    enrolledName.value = null
    const result = await request<Invitation>('/api/admin/enrolment/invitations', {
      method: 'POST',
      body: serverPersonId === undefined ? {} : { serverPersonId },
    })
    if (result.kind === 'ok') {
      invitation.value = result.data
    }
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
    invitation,
    enrolledName,
    load,
    rename,
    revokeDevice,
    deactivate,
    createInvitation,
    closeInvitation,
    listen,
  }
})
