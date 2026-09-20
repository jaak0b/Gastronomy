import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { request, requestAction, type ApiResult } from '../../api/client'
import { adminFestivalsResponseSchema } from '../../core/apiSchemas'
import { adminErrorMessage } from '../../core/adminErrorMessage'
import {
  adminFailed,
  adminOk,
  type AdminActionResult,
} from '../../core/adminActionResult'
import type { AdminFestival } from '../../core/apiTypes'
import { createLatestRequestGate } from '../../core/latestRequestGate'
import { useConnectionStore } from '../connection'

export interface FestivalDraft {
  name: string
  startsAtUtc: string
  endsAtUtc: string
}

export const useAdminFestivalsStore = defineStore('adminFestivals', () => {
  const festivals = ref<AdminFestival[]>([])
  const loadFailed = ref(false)

  const festivalsGate = createLatestRequestGate()

  const shownFestivals = computed(() => festivals.value.filter((festival) => !festival.isHidden))
  const runningFestival = computed<AdminFestival | null>(
    () => festivals.value.find((festival) => festival.isRunning) ?? null,
  )

  function findFestivalWithId(festivalId: string): AdminFestival | null {
    return festivals.value.find((festival) => festival.festivalId === festivalId) ?? null
  }

  async function load(): Promise<void> {
    loadFailed.value = false
    const token = festivalsGate.start()
    const result = await request('/api/admin/festivals', {
      schema: adminFestivalsResponseSchema,
    })
    if (!festivalsGate.isCurrent(token)) {
      return
    }
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    festivals.value = result.data.festivals
  }

  function buildFestivalRequestBody(draft: FestivalDraft): FestivalDraft {
    return { name: draft.name, startsAtUtc: draft.startsAtUtc, endsAtUtc: draft.endsAtUtc }
  }

  async function reportAndReload(result: ApiResult<unknown>): Promise<AdminActionResult<null>> {
    if (result.kind !== 'ok') {
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    await load()
    return adminOk(null)
  }

  async function create(draft: FestivalDraft): Promise<AdminActionResult<null>> {
    const result = await requestAction('/api/admin/festivals', {
      method: 'POST',
      body: buildFestivalRequestBody(draft),
    })
    return reportAndReload(result)
  }

  async function save(festivalId: string, draft: FestivalDraft): Promise<AdminActionResult<null>> {
    const result = await requestAction(`/api/admin/festivals/${festivalId}`, {
      method: 'PUT',
      body: buildFestivalRequestBody(draft),
    })
    return reportAndReload(result)
  }

  async function copy(festivalId: string, draft: FestivalDraft): Promise<AdminActionResult<null>> {
    const result = await requestAction(`/api/admin/festivals/${festivalId}/copy`, {
      method: 'POST',
      body: buildFestivalRequestBody(draft),
    })
    return reportAndReload(result)
  }

  async function hide(festivalId: string): Promise<AdminActionResult<null>> {
    const result = await requestAction(`/api/admin/festivals/${festivalId}/hide`, {
      method: 'POST',
    })
    return reportAndReload(result)
  }

  async function show(festivalId: string): Promise<AdminActionResult<null>> {
    const result = await requestAction(`/api/admin/festivals/${festivalId}/show`, {
      method: 'POST',
    })
    return reportAndReload(result)
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(load),
      connection.onEvent<unknown>('FestivalChanged', () => {
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
