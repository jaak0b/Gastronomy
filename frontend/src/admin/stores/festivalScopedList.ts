import { ref, type Ref } from 'vue'
import type { z } from 'zod'
import { assertNever } from '../../shared/core/assertNever'
import { adminFailed, adminOk, type AdminActionResult } from '../core/adminActionResult'
import { defineAdminList } from './adminList'

export interface FestivalScopedListDefinition<TEntry, TListResponse, TDraft, TBody> {
  path: string
  listSchema: z.ZodType<TListResponse>
  entrySchema: z.ZodType<TEntry>
  entriesOf: (response: TListResponse) => TEntry[]
  idOf: (entry: TEntry) => string
  draftIdOf: (draft: TDraft) => string | undefined
  requestBodyOf: (draft: TDraft) => TBody
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

export function defineFestivalScopedList<TEntry, TListResponse, TDraft, TBody>(
  definition: FestivalScopedListDefinition<TEntry, TListResponse, TDraft, TBody>,
): FestivalScopedList<TEntry, TDraft> {
  const festivalInView = ref<string | null>(null)
  const { entries, loadFailed, loadFrom, forgetCreatedEntries, create, update, setActive } =
    defineAdminList({
      path: definition.path,
      listSchema: definition.listSchema,
      entrySchema: definition.entrySchema,
      entriesOf: definition.entriesOf,
      idOf: definition.idOf,
      requestBodyOf: definition.requestBodyOf,
      reloadAfterWriting: reload,
      currentView: () => festivalInView.value,
    })

  async function load(): Promise<void> {
    if (festivalInView.value !== null) {
      forgetCreatedEntries()
    }
    festivalInView.value = null
    await loadFrom(definition.path)
  }

  async function loadAtTheFestival(festivalId: string): Promise<void> {
    if (festivalInView.value !== festivalId) {
      forgetCreatedEntries()
    }
    festivalInView.value = festivalId
    await loadFrom(`${definition.path}?festivalId=${festivalId}`)
  }

  async function reload(): Promise<void> {
    const festivalId = festivalInView.value
    if (festivalId === null) {
      await load()
      return
    }
    await loadAtTheFestival(festivalId)
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
    return await update(id, draft)
  }

  return { entries, loadFailed, load, loadAtTheFestival, reload, create, save, setActive }
}
