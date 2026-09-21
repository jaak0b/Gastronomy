import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { requestAction } from '../../shared/api/client'
import { AdminFestivalListView, AdminFestivalView } from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { reloadOrFailureOf } from '../core/adminMutation'
import { loadAdminList } from '../core/adminList'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'

export interface FestivalDraft {
  name: string
  startsAtUtc: string
  endsAtUtc: string
}

export const useAdminFestivalsStore = defineStore('adminFestivals', () => {
  const festivals = ref<AdminFestivalView[]>([])
  const loadFailed = ref(false)

  const festivalsGate = createLatestRequestGate()

  const shownFestivals = computed(() => festivals.value.filter((festival) => !festival.isHidden))
  const runningFestival = computed<AdminFestivalView | null>(
    () => festivals.value.find((festival) => festival.isRunning) ?? null,
  )

  function findFestivalWithId(festivalId: string): AdminFestivalView | null {
    return festivals.value.find((festival) => festival.festivalId === festivalId) ?? null
  }

  async function load(): Promise<void> {
    await loadAdminList({
      path: '/api/admin/festivals',
      schema: AdminFestivalListView,
      gate: festivalsGate,
      itemsOf: (response) => response.festivals,
      showItems: (loaded) => {
        festivals.value = loaded
      },
      setLoadFailed: (failed) => {
        loadFailed.value = failed
      },
    })
  }

  function buildFestivalRequestBody(draft: FestivalDraft): FestivalDraft {
    return { name: draft.name, startsAtUtc: draft.startsAtUtc, endsAtUtc: draft.endsAtUtc }
  }

  async function create(draft: FestivalDraft): Promise<AdminActionResult<null>> {
    const result = await requestAction('/api/admin/festivals', {
      method: 'POST',
      body: buildFestivalRequestBody(draft),
    })
    return await reloadOrFailureOf(result, load)
  }

  async function save(festivalId: string, draft: FestivalDraft): Promise<AdminActionResult<null>> {
    const result = await requestAction(`/api/admin/festivals/${festivalId}`, {
      method: 'PUT',
      body: buildFestivalRequestBody(draft),
    })
    return await reloadOrFailureOf(result, load)
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
