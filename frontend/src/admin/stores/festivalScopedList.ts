import { ref, type Ref } from 'vue'
import type { z } from 'zod'
import { request, requestAction } from '../../shared/api/client'
import { assertNever } from '../../shared/core/assertNever'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { adminFailed, adminOk, type AdminActionResult } from '../core/adminActionResult'
import { loadAdminList } from '../core/adminList'
import { adminFailureFrom, reloadOrFailureOf } from '../core/adminMutation'
import { createPendingCreatedEntities } from '../core/pendingCreatedEntities'

export interface FestivalScopedListDefinition<TEntry, TListResponse, TDraft> {
  path: string
  listSchema: z.ZodType<TListResponse>
  entrySchema: z.ZodType<TEntry>
  entriesOf: (response: TListResponse) => TEntry[]
  idOf: (entry: TEntry) => string
  draftIdOf: (draft: TDraft) => string | undefined
  requestBodyOf: (draft: TDraft) => Record<string, unknown>
}

export interface FestivalScopedList<TEntry, TDraft> {
  entries: Ref<TEntry[]>
  loadFailed: Ref<boolean>
  load: () => Promise<void>
  loadAtTheFestival: (festivalId: string) => Promise<void>
  reload: () => Promise<void>
  create: (draft: TDraft) => Promise<AdminActionResult<TEntry>>
  save: (draft: TDraft) => Promise<AdminActionResult<null>>
  setActive: (id: string, isActive: boolean) => Promise<AdminActionResult<null>>
}

export function defineFestivalScopedList<TEntry, TListResponse, TDraft>(
  definition: FestivalScopedListDefinition<TEntry, TListResponse, TDraft>,
): FestivalScopedList<TEntry, TDraft> {
  const entries = ref<TEntry[]>([]) as Ref<TEntry[]>
  const loadFailed = ref(false)
  const festivalInView = ref<string | null>(null)

  const entriesGate = createLatestRequestGate()
  const pendingCreatedEntries = createPendingCreatedEntities<TEntry>(definition.idOf)

  async function loadEntriesFrom(path: string): Promise<void> {
    await loadAdminList({
      path,
      schema: definition.listSchema,
      gate: entriesGate,
      itemsOf: definition.entriesOf,
      showItems: (loaded) => {
        entries.value = pendingCreatedEntries.mergeInto(loaded)
      },
      setLoadFailed: (failed) => {
        loadFailed.value = failed
      },
    })
  }

  async function load(): Promise<void> {
    if (festivalInView.value !== null) {
      pendingCreatedEntries.clear()
    }
    festivalInView.value = null
    await loadEntriesFrom(definition.path)
  }

  async function loadAtTheFestival(festivalId: string): Promise<void> {
    if (festivalInView.value !== festivalId) {
      pendingCreatedEntries.clear()
    }
    festivalInView.value = festivalId
    await loadEntriesFrom(`${definition.path}?festivalId=${festivalId}`)
  }

  async function reload(): Promise<void> {
    const festivalId = festivalInView.value
    if (festivalId === null) {
      await load()
      return
    }
    await loadAtTheFestival(festivalId)
  }

  async function create(draft: TDraft): Promise<AdminActionResult<TEntry>> {
    const scopeAtStart = festivalInView.value
    const result = await request(definition.path, {
      method: 'POST',
      body: definition.requestBodyOf(draft),
      schema: definition.entrySchema,
    })
    if (result.kind !== 'ok') {
      return adminFailureFrom(result)
    }
    if (scopeAtStart === festivalInView.value) {
      pendingCreatedEntries.remember(result.data)
      entries.value = pendingCreatedEntries.mergeInto(entries.value)
    }
    return adminOk(result.data)
  }

  async function save(draft: TDraft): Promise<AdminActionResult<null>> {
    const id = definition.draftIdOf(draft)
    if (id === undefined) {
      const created = await create(draft)
      switch (created.kind) {
        case 'ok':
          return adminOk(null)
        case 'failed':
          return adminFailed(created.message)
        default:
          return assertNever(created)
      }
    }
    return await reloadOrFailureOf(
      await requestAction(`${definition.path}/${id}`, {
        method: 'PUT',
        body: definition.requestBodyOf(draft),
      }),
      reload,
    )
  }

  async function setActive(id: string, isActive: boolean): Promise<AdminActionResult<null>> {
    if (isActive) {
      return await reloadOrFailureOf(
        await requestAction(`${definition.path}/${id}/activate`, { method: 'POST' }),
        reload,
      )
    }
    return await reloadOrFailureOf(
      await requestAction(`${definition.path}/${id}/deactivate`, { method: 'POST' }),
      reload,
    )
  }

  return { entries, loadFailed, load, loadAtTheFestival, reload, create, save, setActive }
}
