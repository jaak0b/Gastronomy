import { computed, shallowRef } from 'vue'
import { assertNever } from '../../shared/core/assertNever'
import type { AdminActionResult } from '../core/adminActionResult'
import { useRefusalDisplay } from './useRefusalDisplay'

export type EditSessionMode = 'create' | 'edit'

export interface EditSessionOptions<TEdited, TDraft, TSaved> {
  saveThrough: (draft: TDraft, edited: TEdited | null) => Promise<AdminActionResult<TSaved>>
  afterSaving?: (saved: TSaved, mode: EditSessionMode) => void
}

export function useEditSession<TEdited, TDraft, TSaved = null>(
  options: EditSessionOptions<TEdited, TDraft, TSaved>,
) {
  const edited = shallowRef<TEdited | null>(null)
  const isCreating = shallowRef(false)
  const { refusal, refusalText, showRefusalOf } = useRefusalDisplay()

  const isOpen = computed(() => isCreating.value || edited.value !== null)

  function forgetRefusal(): void {
    refusal.value = null
  }

  function openForCreate(): void {
    forgetRefusal()
    edited.value = null
    isCreating.value = true
  }

  function openForEdit(entity: TEdited): void {
    forgetRefusal()
    isCreating.value = false
    edited.value = entity
  }

  function close(): void {
    forgetRefusal()
    isCreating.value = false
    edited.value = null
  }

  async function save(draft: TDraft): Promise<void> {
    if (!isOpen.value) {
      return
    }
    forgetRefusal()
    const mode: EditSessionMode = isCreating.value ? 'create' : 'edit'
    const saved = await options.saveThrough(draft, edited.value)
    switch (saved.kind) {
      case 'ok':
        close()
        options.afterSaving?.(saved.value, mode)
        return
      case 'failed':
        refusal.value = saved.message
        return
      default:
        assertNever(saved)
    }
  }

  return {
    edited,
    isCreating,
    isOpen,
    refusal,
    refusalText,
    showRefusalOf,
    forgetRefusal,
    openForCreate,
    openForEdit,
    close,
    save,
  }
}
