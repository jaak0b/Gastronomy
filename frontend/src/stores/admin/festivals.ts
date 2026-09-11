import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { listFrom, request, type ApiResult } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
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
  const errorMessage = ref<AdminErrorMessage | null>(null)

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

  async function reportAndReload(result: ApiResult<unknown>): Promise<boolean> {
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return false
    }
    await load()
    return true
  }

  async function create(draft: FestivalDraft): Promise<boolean> {
    errorMessage.value = null
    const result = await request('/api/admin/festivals', {
      method: 'POST',
      body: bodyOf(draft),
    })
    return reportAndReload(result)
  }

  async function save(festivalId: string, draft: FestivalDraft): Promise<boolean> {
    errorMessage.value = null
    const result = await request(`/api/admin/festivals/${festivalId}`, {
      method: 'PUT',
      body: bodyOf(draft),
    })
    return reportAndReload(result)
  }

  async function copy(festivalId: string, draft: FestivalDraft): Promise<boolean> {
    errorMessage.value = null
    const result = await request(`/api/admin/festivals/${festivalId}/copy`, {
      method: 'POST',
      body: bodyOf(draft),
    })
    return reportAndReload(result)
  }

  async function hide(festivalId: string): Promise<boolean> {
    errorMessage.value = null
    const result = await request(`/api/admin/festivals/${festivalId}/hide`, { method: 'POST' })
    return reportAndReload(result)
  }

  async function show(festivalId: string): Promise<boolean> {
    errorMessage.value = null
    const result = await request(`/api/admin/festivals/${festivalId}/show`, { method: 'POST' })
    return reportAndReload(result)
  }

  function listen(): () => void {
    return useConnectionStore().registerRefetch(load)
  }

  function forgetError(): void {
    errorMessage.value = null
  }

  return {
    festivals,
    shownFestivals,
    runningFestival,
    loadFailed,
    errorMessage,
    festivalWithId,
    load,
    create,
    save,
    copy,
    hide,
    show,
    forgetError,
    listen,
  }
})
