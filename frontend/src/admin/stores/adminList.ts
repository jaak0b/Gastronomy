import { shallowRef, ref, type Ref } from 'vue'
import type { z } from 'zod'
import { request, requestAction } from '../../shared/api/client'
import {
  createLatestRequestGate,
  type LatestRequestGate,
} from '../../shared/core/latestRequestGate'
import { adminOk, type AdminActionResult } from '../core/adminActionResult'
import { loadAdminList } from '../core/listLoading'
import { adminFailureFrom, reloadOrFailureOf } from '../core/adminMutation'
import { createPendingCreatedEntities } from '../core/pendingCreatedEntities'

export interface AdminListDefinition<TEntry, TListResponse, TDraft, TBody> {
  path: string
  listSchema: z.ZodType<TListResponse>
  entrySchema: z.ZodType<TEntry>
  entriesOf: (response: TListResponse) => TEntry[]
  idOf: (entry: TEntry) => string
  requestBodyOf: (draft: TDraft) => TBody
  reloadAfterWriting?: () => Promise<void>
  currentView?: () => string | null
  afterShowingEntries?: () => void
}

export interface AdminList<TEntry, TDraft> {
  entries: Ref<TEntry[]>
  hasLoaded: Ref<boolean>
  loadFailed: Ref<boolean>
  entriesGate: LatestRequestGate
  load: () => Promise<void>
  loadFrom: (path: string) => Promise<void>
  forgetCreatedEntries: () => void
  showCreated: (entry: TEntry) => void
  update: (id: string, draft: TDraft) => Promise<AdminActionResult<null>>
}

export interface Creating<TEntry, TDraft> {
  create: (draft: TDraft) => Promise<AdminActionResult<TEntry>>
  createThenReload: (draft: TDraft) => Promise<AdminActionResult<null>>
}

export interface Activation {
  setActive: (id: string, isActive: boolean) => Promise<AdminActionResult<null>>
}

export function adminListDefinition<TEntry, TListResponse, TDraft, TBody>(
  definition: AdminListDefinition<TEntry, TListResponse, TDraft, TBody>,
): AdminListDefinition<TEntry, TListResponse, TDraft, TBody> {
  return definition
}

export function defineAdminList<TEntry, TListResponse, TDraft, TBody>(
  definition: AdminListDefinition<TEntry, TListResponse, TDraft, TBody>,
): AdminList<TEntry, TDraft> {
  const entries = shallowRef<TEntry[]>([])
  const hasLoaded = ref(false)
  const loadFailed = ref(false)

  const entriesGate = createLatestRequestGate()
  const pendingCreatedEntries = createPendingCreatedEntities<TEntry>(definition.idOf)
  const reloadAfterWriting = definition.reloadAfterWriting ?? load

  async function loadFrom(path: string): Promise<void> {
    await loadAdminList({
      path,
      schema: definition.listSchema,
      gate: entriesGate,
      itemsOf: definition.entriesOf,
      showItems: (loaded) => {
        entries.value = pendingCreatedEntries.mergeInto(loaded)
        hasLoaded.value = true
        definition.afterShowingEntries?.()
      },
      setLoadFailed: (failed) => {
        loadFailed.value = failed
      },
    })
  }

  async function load(): Promise<void> {
    await loadFrom(definition.path)
  }

  function forgetCreatedEntries(): void {
    pendingCreatedEntries.clear()
  }

  function showCreated(entry: TEntry): void {
    pendingCreatedEntries.remember(entry)
    entries.value = pendingCreatedEntries.mergeInto(entries.value)
  }

  async function update(id: string, draft: TDraft): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`${definition.path}/${id}`, {
        method: 'PUT',
        body: definition.requestBodyOf(draft),
      }),
      reloadAfterWriting,
    )
  }

  return {
    entries,
    hasLoaded,
    loadFailed,
    entriesGate,
    load,
    loadFrom,
    forgetCreatedEntries,
    showCreated,
    update,
  }
}

export function withCreating<
  TEntry,
  TListResponse,
  TDraft,
  TBody,
  TList extends AdminList<TEntry, TDraft>,
>(
  list: TList,
  definition: AdminListDefinition<TEntry, TListResponse, TDraft, TBody>,
): TList & Creating<TEntry, TDraft> {
  const reloadAfterWriting = definition.reloadAfterWriting ?? list.load
  const currentView = definition.currentView ?? (() => null)

  async function create(draft: TDraft): Promise<AdminActionResult<TEntry>> {
    const viewAtStart = currentView()
    const result = await request(definition.path, {
      method: 'POST',
      body: definition.requestBodyOf(draft),
      schema: definition.entrySchema,
    })
    if (result.kind !== 'ok') {
      return adminFailureFrom(result)
    }
    if (viewAtStart === currentView()) {
      list.showCreated(result.data)
    }
    return adminOk(result.data)
  }

  async function createThenReload(draft: TDraft): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(definition.path, {
        method: 'POST',
        body: definition.requestBodyOf(draft),
      }),
      reloadAfterWriting,
    )
  }

  return { ...list, create, createThenReload }
}

export function withActivation<
  TEntry,
  TListResponse,
  TDraft,
  TBody,
  TList extends AdminList<TEntry, TDraft>,
>(
  list: TList,
  definition: AdminListDefinition<TEntry, TListResponse, TDraft, TBody>,
): TList & Activation {
  const reloadAfterWriting = definition.reloadAfterWriting ?? list.load

  async function setActive(id: string, isActive: boolean): Promise<AdminActionResult<null>> {
    const action = isActive ? 'activate' : 'deactivate'
    return await reloadOrFailureOf(
      await requestAction(`${definition.path}/${id}/${action}`, { method: 'POST' }),
      reloadAfterWriting,
    )
  }

  return { ...list, setActive }
}
