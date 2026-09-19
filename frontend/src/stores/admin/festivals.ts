import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { listFrom, request, type ApiResult } from '../../api/client'
import { adminErrorMessage } from '../../core/adminErrorMessage'
import {
  adminFailed,
  adminOk,
  type AdminActionResult,
} from '../../core/adminActionResult'
import { useConnectionStore } from '../connection'

export interface AdminFestival {
  festivalId: string
  name: string
  startsAtUtc: string
  endsAtUtc: string
  isHidden: boolean
  isRunning: boolean
  stationCount: number
  menuItemCount: number
  orderCount: number
}

export interface FestivalDraft {
  name: string
  startsAtUtc: string
  endsAtUtc: string
}

export const useAdminFestivalsStore = defineStore('adminFestivals', () => {
  const festivals = ref<AdminFestival[]>([])
  const loadFailed = ref(false)

  const shownFestivals = computed(() => festivals.value.filter((festival) => !festival.isHidden))
  const runningFestival = computed<AdminFestival | null>(
    () => festivals.value.find((festival) => festival.isRunning) ?? null,
  )

  function festivalWithId(festivalId: string): AdminFestival | null {
    return festivals.value.find((festival) => festival.festivalId === festivalId) ?? null
  }

  async function load(): Promise<void> {
    loadFailed.value = false
    const result = await request<unknown>('/api/admin/festivals')
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    const rows = listFrom<AdminFestival>(result.data, 'festivals')
    if (rows === null) {
      loadFailed.value = true
      return
    }
    festivals.value = rows
  }

  function bodyOf(draft: FestivalDraft): FestivalDraft {
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
    const result = await request('/api/admin/festivals', {
      method: 'POST',
      body: bodyOf(draft),
    })
    return reportAndReload(result)
  }

  async function save(festivalId: string, draft: FestivalDraft): Promise<AdminActionResult<null>> {
    const result = await request(`/api/admin/festivals/${festivalId}`, {
      method: 'PUT',
      body: bodyOf(draft),
    })
    return reportAndReload(result)
  }

  async function copy(festivalId: string, draft: FestivalDraft): Promise<AdminActionResult<null>> {
    const result = await request(`/api/admin/festivals/${festivalId}/copy`, {
      method: 'POST',
      body: bodyOf(draft),
    })
    return reportAndReload(result)
  }

  async function hide(festivalId: string): Promise<AdminActionResult<null>> {
    const result = await request(`/api/admin/festivals/${festivalId}/hide`, { method: 'POST' })
    return reportAndReload(result)
  }

  async function show(festivalId: string): Promise<AdminActionResult<null>> {
    const result = await request(`/api/admin/festivals/${festivalId}/show`, { method: 'POST' })
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
    festivalWithId,
    load,
    create,
    save,
    copy,
    hide,
    show,
    listen,
  }
})
