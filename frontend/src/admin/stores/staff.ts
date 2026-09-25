import { defineStore } from 'pinia'
import { ref } from 'vue'
import { requestAction } from '../../shared/api/client'
import { AdminStaffMemberListView, AdminStaffMemberView } from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { reloadOrFailureOf } from '../core/adminMutation'
import { loadAdminList } from '../core/adminList'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'
import { useAdminEnrolmentStore } from './enrolment'

export const useAdminStaffStore = defineStore('adminStaff', () => {
  const staffMembers = ref<AdminStaffMemberView[]>([])
  const loadFailed = ref(false)

  const staffMembersGate = createLatestRequestGate()

  async function load(): Promise<void> {
    await loadAdminList({
      path: '/api/admin/staff-members',
      schema: AdminStaffMemberListView,
      gate: staffMembersGate,
      itemsOf: (response) => response.staffMembers,
      showItems: (loaded) => {
        staffMembers.value = loaded
      },
      setLoadFailed: (failed) => {
        loadFailed.value = failed
      },
    })
  }

  async function rename(id: string, name: string): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/staff-members/${id}`, { method: 'PUT', body: { name } }),
      load,
    )
  }

  async function setActive(id: string, isActive: boolean): Promise<AdminActionResult<null>> {
    const action = isActive ? 'activate' : 'deactivate'
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/staff-members/${id}/${action}`, { method: 'POST' }),
      load,
    )
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(load),
      connection.onEvent<unknown>('ConfigurationChanged', () => {
        void load()
      }),
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
