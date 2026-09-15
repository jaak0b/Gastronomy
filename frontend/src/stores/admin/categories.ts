import { defineStore } from 'pinia'
import { ref } from 'vue'
import { listFrom, request } from '../../api/client'
import { adminErrorMessage } from '../../core/adminErrorMessage'
import {
  adminFailed,
  adminOk,
  type AdminActionResult,
} from '../../core/adminActionResult'
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
  let latestMove: Promise<AdminActionResult<null>> = Promise.resolve(adminOk(null))

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

  async function create(draft: AdminCategoryDraft): Promise<AdminActionResult<AdminCategory>> {
    const result = await request<AdminCategory>('/api/admin/categories', {
      method: 'POST',
      body: { name: draft.name, colourHex: draft.colourHex },
    })
    if (result.kind !== 'ok') {
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    categories.value = [...categories.value, result.data]
    return adminOk(result.data)
  }

  async function save(
    category: AdminCategoryDraft & { categoryId: string },
  ): Promise<AdminActionResult<null>> {
    const result = await request(`/api/admin/categories/${category.categoryId}`, {
      method: 'PUT',
      body: { name: category.name, colourHex: category.colourHex },
    })
    if (result.kind !== 'ok') {
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    await load()
    return adminOk(null)
  }

  async function move(
    categoryId: string,
    direction: CategoryMoveDirection,
  ): Promise<AdminActionResult<null>> {
    latestMove = latestMove.then(() => sendMove(categoryId, direction))
    return await latestMove
  }

  async function sendMove(
    categoryId: string,
    direction: CategoryMoveDirection,
  ): Promise<AdminActionResult<null>> {
    const result = await request<unknown>(`/api/admin/categories/${categoryId}/move`, {
      method: 'POST',
      body: { direction },
    })
    if (result.kind !== 'ok') {
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    const rows = listFrom<AdminCategory>(result.data, 'categories')
    if (rows === null) {
      await load()
      return adminFailed(adminErrorMessage(null))
    }
    categories.value = rows
    return adminOk(null)
  }

  async function setActive(
    categoryId: string,
    isActive: boolean,
  ): Promise<AdminActionResult<null>> {
    const action = isActive ? 'activate' : 'deactivate'
    const result = await request(`/api/admin/categories/${categoryId}/${action}`, {
      method: 'POST',
    })
    if (result.kind !== 'ok') {
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    await load()
    return adminOk(null)
  }

  function listen(): () => void {
    return useConnectionStore().registerRefetch(load)
  }

  return {
    categories,
    loadFailed,
    load,
    create,
    save,
    move,
    setActive,
    listen,
  }
})
