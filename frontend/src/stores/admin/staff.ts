import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request, requestAction } from '../../shared/api/client'
import { adminStaffMembersResponseSchema } from '../../shared/api/apiSchemas'
import { adminErrorMessage } from '../../core/adminErrorMessage'
import {
  adminFailed,
  adminOk,
  type AdminActionResult,
} from '../../core/adminActionResult'
import type { AdminStaffMember } from '../../shared/api/apiTypes'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'
import { useAdminEnrolmentStore } from './enrolment'

export const useAdminStaffStore = defineStore('adminStaff', () => {
  const staffMembers = ref<AdminStaffMember[]>([])
  const loadFailed = ref(false)

  const staffMembersGate = createLatestRequestGate()

  async function load(): Promise<void> {
    loadFailed.value = false
    const token = staffMembersGate.startRequest()
    const result = await request('/api/admin/staff-members', {
      schema: adminStaffMembersResponseSchema,
    })
    if (!staffMembersGate.isNewestRequest(token)) {
      return
    }
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    staffMembers.value = result.data.staffMembers
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
    const result = await requestAction(path, { method, body })
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
