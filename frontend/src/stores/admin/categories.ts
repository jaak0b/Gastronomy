import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request, requestAction } from '../../shared/api/client'
import { adminCategoriesResponseSchema, adminCategorySchema } from '../../shared/api/apiSchemas'
import { adminErrorMessage } from '../../core/adminErrorMessage'
import {
  adminFailed,
  adminOk,
  type AdminActionResult,
} from '../../core/adminActionResult'
import type { AdminCategory } from '../../shared/api/apiTypes'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'

export interface AdminCategoryDraft {
  name: string
  colourHex: string
}

export type CategoryMoveDirection = 'up' | 'down'

export const useAdminCategoriesStore = defineStore('adminCategories', () => {
  const categories = ref<AdminCategory[]>([])
  const loadFailed = ref(false)

  const categoriesGate = createLatestRequestGate()
  let latestMove: Promise<AdminActionResult<null>> = Promise.resolve(adminOk(null))

  async function load(): Promise<void> {
    loadFailed.value = false
    const token = categoriesGate.startRequest()
    const result = await request('/api/admin/categories', {
      schema: adminCategoriesResponseSchema,
    })
    if (!categoriesGate.isNewestRequest(token)) {
      return
    }
    if (result.kind !== 'ok') {
      loadFailed.value = true
      return
    }
    categories.value = result.data.categories
  }

  async function create(draft: AdminCategoryDraft): Promise<AdminActionResult<AdminCategory>> {
    const token = categoriesGate.startRequest()
    const result = await request('/api/admin/categories', {
      method: 'POST',
      body: { name: draft.name, colourHex: draft.colourHex },
      schema: adminCategorySchema,
    })
    if (result.kind !== 'ok') {
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    if (categoriesGate.isNewestRequest(token)) {
      categories.value = [...categories.value, result.data]
    }
    return adminOk(result.data)
  }

  async function save(
    category: AdminCategoryDraft & { categoryId: string },
  ): Promise<AdminActionResult<null>> {
    const result = await requestAction(`/api/admin/categories/${category.categoryId}`, {
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
    const token = categoriesGate.startRequest()
    const result = await request(`/api/admin/categories/${categoryId}/move`, {
      method: 'POST',
      body: { direction },
      schema: adminCategoriesResponseSchema,
    })
    if (result.kind === 'unreadableAnswer') {
      await load()
      return adminFailed(adminErrorMessage(null))
    }
    if (result.kind !== 'ok') {
      return adminFailed(adminErrorMessage(result.kind === 'error' ? result.body : null))
    }
    if (categoriesGate.isNewestRequest(token)) {
      categories.value = result.data.categories
    }
    return adminOk(null)
  }

  async function setActive(
    categoryId: string,
    isActive: boolean,
  ): Promise<AdminActionResult<null>> {
    const action = isActive ? 'activate' : 'deactivate'
    const result = await requestAction(`/api/admin/categories/${categoryId}/${action}`, {
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
