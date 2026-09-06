import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import type { StaffMember } from '../../core/apiTypes'
import { useConnectionStore } from '../connection'
import { useAdminEnrolmentStore } from './enrolment'

type Committed<T> = { accepted: true; data: T } | { accepted: false }

export interface AdminStaffMember {
  staffMemberId: string
  name: string
  isActive: boolean
  hasDevice: boolean
  lastSeenAtUtc: string | null
  hasOutstandingInvitation: boolean
}

export const useAdminStaffStore = defineStore('adminStaff', () => {
  const staffMembers = ref<AdminStaffMember[]>([])
  const loadFailed = ref(false)
  const errorMessage = ref<AdminErrorMessage | null>(null)

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

  async function create(name: string): Promise<string | null> {
    const written = await commit<StaffMember>('/api/admin/staff-members', 'POST', { name })
    return written.accepted ? written.data.id : null
  }

  async function rename(id: string, name: string): Promise<boolean> {
    return (await commit(`/api/admin/staff-members/${id}`, 'PUT', { name })).accepted
  }

  async function setActive(id: string, isActive: boolean): Promise<boolean> {
    const action = isActive ? 'activate' : 'deactivate'
    return (await commit(`/api/admin/staff-members/${id}/${action}`, 'POST', undefined)).accepted
  }

  async function commit<T>(
    path: string,
    method: 'POST' | 'PUT',
    body: unknown,
  ): Promise<Committed<T>> {
    errorMessage.value = null
    const result = await request<T>(path, { method, body })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return { accepted: false }
    }
    await load()
    return { accepted: true, data: result.data }
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(load),
      useAdminEnrolmentStore().listen(() => {
        void load()
      }),
    ]
    return () => {
      for (const release of releases) {
        release()
      }
    }
  }

  return {
    staffMembers,
    loadFailed,
    errorMessage,
    load,
    create,
    rename,
    setActive,
    listen,
  }
})
