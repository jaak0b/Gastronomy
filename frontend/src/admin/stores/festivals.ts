import { defineStore } from 'pinia'
import { computed } from 'vue'
import { requestAction } from '../../shared/api/client'
import {
  AdminFestivalListView,
  AdminFestivalView,
  type SaveFestivalRequest,
} from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { reloadOrFailureOf } from '../core/adminMutation'
import { useConnectionStore } from '../../shared/stores/connection'
import { defineAdminList } from './adminList'

export interface FestivalDraft {
  name: string
  startsAtUtc: string
  endsAtUtc: string
}

function buildFestivalRequestBody(draft: FestivalDraft): SaveFestivalRequest {
  return { name: draft.name, startsAtUtc: draft.startsAtUtc, endsAtUtc: draft.endsAtUtc }
}

export const useAdminFestivalsStore = defineStore('adminFestivals', () => {
  const {
    entries: festivals,
    loadFailed,
    load,
    createThenReload: create,
    update,
  } = defineAdminList({
    path: '/api/admin/festivals',
    listSchema: AdminFestivalListView,
    entrySchema: AdminFestivalView,
    entriesOf: (response) => response.festivals,
    idOf: (festival) => festival.festivalId,
    requestBodyOf: buildFestivalRequestBody,
  })

  const shownFestivals = computed(() => festivals.value.filter((festival) => !festival.isHidden))
  const runningFestival = computed<AdminFestivalView | null>(
    () => festivals.value.find((festival) => festival.isRunning) ?? null,
  )

  function findFestivalWithId(festivalId: string): AdminFestivalView | null {
    return festivals.value.find((festival) => festival.festivalId === festivalId) ?? null
  }

  async function save(festivalId: string, draft: FestivalDraft): Promise<AdminActionResult<null>> {
    return await update(festivalId, draft)
  }

  async function copy(festivalId: string, draft: FestivalDraft): Promise<AdminActionResult<null>> {
    const result = await requestAction(`/api/admin/festivals/${festivalId}/copy`, {
      method: 'POST',
      body: buildFestivalRequestBody(draft),
    })
    return await reloadOrFailureOf(result, load)
  }

  async function hide(festivalId: string): Promise<AdminActionResult<null>> {
    const result = await requestAction(`/api/admin/festivals/${festivalId}/hide`, {
      method: 'POST',
    })
    return await reloadOrFailureOf(result, load)
  }

  async function show(festivalId: string): Promise<AdminActionResult<null>> {
    const result = await requestAction(`/api/admin/festivals/${festivalId}/show`, {
      method: 'POST',
    })
    return await reloadOrFailureOf(result, load)
  }

  function listen(): () => void {
    return useConnectionStore().listenToTheLaptop(['ConfigurationChanged', 'OrdersChanged'], load)
  }

  return {
    festivals,
    shownFestivals,
    runningFestival,
    loadFailed,
    findFestivalWithId,
    load,
    create,
    save,
    copy,
    hide,
    show,
    listen,
  }
})
