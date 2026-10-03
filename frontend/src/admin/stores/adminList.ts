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
}

export interface AdminList<TEntry, TDraft> {
  entries: Ref<TEntry[]>
  loadFailed: Ref<boolean>
  entriesGate: LatestRequestGate
  load: () => Promise<void>
  loadFrom: (path: string) => Promise<void>
  forgetCreatedEntries: () => void
  create: (draft: TDraft) => Promise<AdminActionResult<TEntry>>
  createThenReload: (draft: TDraft) => Promise<AdminActionResult<null>>
  update: (id: string, draft: TDraft) => Promise<AdminActionResult<null>>
  setActive: (id: string, isActive: boolean) => Promise<AdminActionResult<null>>
}

export function defineAdminList<TEntry, TListResponse, TDraft, TBody>(
  definition: AdminListDefinition<TEntry, TListResponse, TDraft, TBody>,
): AdminList<TEntry, TDraft> {
  const entries = shallowRef<TEntry[]>([])
  const loadFailed = ref(false)

  const entriesGate = createLatestRequestGate()
  const pendingCreatedEntries = createPendingCreatedEntities<TEntry>(definition.idOf)
  const reloadAfterWriting = definition.reloadAfterWriting ?? load
  const currentView = definition.currentView ?? (() => null)

  async function loadFrom(path: string): Promise<void> {
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
    await loadFrom(definition.path)
  }

  function forgetCreatedEntries(): void {
    pendingCreatedEntries.clear()
  }

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
      pendingCreatedEntries.remember(result.data)
      entries.value = pendingCreatedEntries.mergeInto(entries.value)
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

  async function update(id: string, draft: TDraft): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`${definition.path}/${id}`, {
        method: 'PUT',
        body: definition.requestBodyOf(draft),
      }),
      reloadAfterWriting,
    )
  }

  async function setActive(id: string, isActive: boolean): Promise<AdminActionResult<null>> {
    const action = isActive ? 'activate' : 'deactivate'
    return await reloadOrFailureOf(
      await requestAction(`${definition.path}/${id}/${action}`, { method: 'POST' }),
      reloadAfterWriting,
    )
  }

  return {
    entries,
    loadFailed,
    entriesGate,
    load,
    loadFrom,
    forgetCreatedEntries,
    create,
    createThenReload,
    update,
    setActive,
  }
}
