import { defineStore } from 'pinia'
import {
  AdminStaffMemberListView,
  AdminStaffMemberView,
  type RenameStaffMemberRequest,
} from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { useConnectionStore } from '../../shared/stores/connection'
import { combineReleases } from '../../shared/core/combineReleases'
import { useAdminEnrolmentStore } from './enrolment'
import { defineAdminList } from './adminList'

interface StaffMemberDraft {
  name: string
}

export const useAdminStaffStore = defineStore('adminStaff', () => {
  const {
    entries: staffMembers,
    loadFailed,
    load,
    update,
    setActive,
  } = defineAdminList({
    path: '/api/admin/staff-members',
    listSchema: AdminStaffMemberListView,
    entrySchema: AdminStaffMemberView,
    entriesOf: (response) => response.staffMembers,
    idOf: (staffMember) => staffMember.staffMemberId,
    requestBodyOf: (draft: StaffMemberDraft): RenameStaffMemberRequest => ({ name: draft.name }),
  })

  async function rename(id: string, name: string): Promise<AdminActionResult<null>> {
    return await update(id, { name })
  }

  function listen(): () => void {
    return combineReleases(
      useConnectionStore().listenToTheLaptop(['ConfigurationChanged'], load),
      useAdminEnrolmentStore().listen(() => {
        void load()
      }),
    )
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
