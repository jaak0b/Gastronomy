import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage, type AdminErrorMessage } from '../../core/adminErrorMessage'
import type { AdminCategory } from '../../core/apiTypes'
import { useConnectionStore } from '../connection'

export interface AdminCategoryDraft {
  name: string
  colourHex: string
}

export type CategoryMoveDirection = 'up' | 'down'

export const useAdminCategoriesStore = defineStore('adminCategories', () => {
  const categories = ref<AdminCategory[]>([])
  const loadFailed = ref(false)
  const errorMessage = ref<AdminErrorMessage | null>(null)
  let latestMove: Promise<void> = Promise.resolve()

  async function load(): Promise<void> {
    loadFailed.value = false
    const result = await request<unknown>('/api/admin/categories')
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    const rows = listFrom<AdminCategory>(result.data, 'categories')
    if (rows === null) {
      loadFailed.value = true
      return
    }
    categories.value = rows
  }

  async function create(draft: AdminCategoryDraft): Promise<AdminCategory | null> {
    errorMessage.value = null
    const result = await request<AdminCategory>('/api/admin/categories', {
      method: 'POST',
      body: { name: draft.name, colourHex: draft.colourHex },
    })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return null
    }
    categories.value = [...categories.value, result.data]
    return result.data
  }

  async function save(category: AdminCategoryDraft & { categoryId: string }): Promise<boolean> {
    errorMessage.value = null
    const result = await request(`/api/admin/categories/${category.categoryId}`, {
      method: 'PUT',
      body: { name: category.name, colourHex: category.colourHex },
    })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return false
    }
    await load()
    return true
  }

  async function move(categoryId: string, direction: CategoryMoveDirection): Promise<void> {
    latestMove = latestMove.then(() => sendMove(categoryId, direction))
    await latestMove
  }

  async function sendMove(categoryId: string, direction: CategoryMoveDirection): Promise<void> {
    errorMessage.value = null
    const result = await request<unknown>(`/api/admin/categories/${categoryId}/move`, {
      method: 'POST',
      body: { direction },
    })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    const rows = listFrom<AdminCategory>(result.data, 'categories')
    if (rows === null) {
      errorMessage.value = adminErrorMessage(null)
      await load()
      return
    }
    categories.value = rows
  }

  async function setActive(categoryId: string, isActive: boolean): Promise<void> {
    errorMessage.value = null
    const action = isActive ? 'activate' : 'deactivate'
    const result = await request(`/api/admin/categories/${categoryId}/${action}`, {
      method: 'POST',
    })
    if (result.kind !== 'ok') {
      errorMessage.value = adminErrorMessage(result.kind === 'error' ? result.body : null)
      return
    }
    await load()
  }

  function forgetError(): void {
    errorMessage.value = null
  }

  function listen(): () => void {
    return useConnectionStore().registerRefetch(load)
  }

  return {
    categories,
    loadFailed,
    errorMessage,
    load,
    create,
    save,
    move,
    setActive,
    forgetError,
    listen,
  }
})
