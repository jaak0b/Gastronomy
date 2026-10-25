import { defineStore } from 'pinia'
import {
  AdminUnitListView,
  AdminUnitView,
  type SaveUnitRequest,
} from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { useConnectionStore } from '../../shared/stores/connection'
import { defineCreatableAdminList } from './adminList'

export interface UnitDraft {
  name: string
}

export const useAdminUnitsStore = defineStore('adminUnits', () => {
  const {
    entries: units,
    loadFailed,
    load,
    create,
    update,
    setActive,
  } = defineCreatableAdminList({
    path: '/api/admin/units',
    listSchema: AdminUnitListView,
    entrySchema: AdminUnitView,
    entriesOf: (response) => response.units,
    idOf: (unit) => unit.unitId,
    requestBodyOf: (draft: UnitDraft): SaveUnitRequest => ({ name: draft.name }),
  })

  async function save(unitId: string, draft: UnitDraft): Promise<AdminActionResult<null>> {
    return await update(unitId, draft)
  }

  function listen(): () => void {
    return useConnectionStore().listenToTheLaptop(['ConfigurationChanged'], load)
  }

  return {
    units,
    loadFailed,
    load,
    create,
    save,
    setActive,
    listen,
  }
})
