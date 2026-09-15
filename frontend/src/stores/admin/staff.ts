import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage } from '../../core/adminErrorMessage'
import {
  adminFailed,
  adminOk,
  type AdminActionResult,
} from '../../core/adminActionResult'
import { useConnectionStore } from '../connection'
import { useAdminEnrolmentStore } from './enrolment'

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

  async function rename(id: string, name: string): Promise<AdminActionResult<null>> {
    return await commit(`/api/admin/staff-members/${id}`, 'PUT', { name })
  }

  async function setActive(id: string, isActive: boolean): Promise<AdminActionResult<null>> {
    const action = isActive ? 'activate' : 'deactivate'
    return await commit(`/api/admin/staff-members/${id}/${action}`, 'POST', undefined)
  }

  async function commit(
    path: string,
    method: 'POST' | 'PUT',
    body: unknown,
  ): Promise<AdminActionResult<null>> {
    const result = await request(path, { method, body })
    if (result.kind !== 'ok') {
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    await load()
    return adminOk(null)
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
    load,
    rename,
    setActive,
    listen,
  }
})
