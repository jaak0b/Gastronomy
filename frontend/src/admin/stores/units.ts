import { defineStore } from 'pinia'
import {
  AdminUnitListView,
  AdminUnitView,
  type SaveUnitRequest,
} from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { useAdminLoadStateStore } from './adminLoadState'
import { adminListDefinition, defineAdminList, withActivation, withCreating } from './adminList'

export interface UnitDraft {
  name: string
}

export const useAdminUnitsStore = defineStore('adminUnits', () => {
  const definition = adminListDefinition({
    path: '/api/admin/units',
    listSchema: AdminUnitListView,
    entrySchema: AdminUnitView,
    entriesOf: (response) => response.units,
    idOf: (unit) => unit.unitId,
    requestBodyOf: (draft: UnitDraft): SaveUnitRequest => ({ name: draft.name }),
  })
  const {
    entries: units,
    loadFailed,
    load,
    create,
    update,
    setActive,
  } = withActivation(withCreating(defineAdminList(definition), definition), definition)

  async function save(unitId: string, draft: UnitDraft): Promise<AdminActionResult<null>> {
    return await update(unitId, draft)
  }

  function listen(): () => void {
    return useAdminLoadStateStore().listenToTheLaptop(['ConfigurationChanged'], load, loadFailed)
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
