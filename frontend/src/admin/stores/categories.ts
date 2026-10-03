import { defineStore } from 'pinia'
import { request } from '../../shared/api/client'
import {
  AdminCategoryListView,
  AdminCategoryView,
  type SaveCategoryRequest,
} from '../../shared/api/generatedSchemas'
import { adminOk, type AdminActionResult } from '../core/adminActionResult'
import { adminFailureFrom } from '../core/adminMutation'
import { useConnectionStore } from '../../shared/stores/connection'
import { defineAdminList } from './adminList'

export interface AdminCategoryDraft {
  name: string
  colourHex: string
}

export type CategoryMoveDirection = 'up' | 'down'

export const useAdminCategoriesStore = defineStore('adminCategories', () => {
  const {
    entries: categories,
    loadFailed,
    entriesGate: categoriesGate,
    load,
    create,
    update,
    setActive,
  } = defineAdminList({
    path: '/api/admin/categories',
    listSchema: AdminCategoryListView,
    entrySchema: AdminCategoryView,
    entriesOf: (response) => response.categories,
    idOf: (category) => category.categoryId,
    requestBodyOf: (draft: AdminCategoryDraft): SaveCategoryRequest => ({ name: draft.name, colourHex: draft.colourHex }),
  })
  let latestMove: Promise<AdminActionResult<null>> = Promise.resolve(adminOk(null))

  async function save(
    category: AdminCategoryDraft & { categoryId: string },
  ): Promise<AdminActionResult<null>> {
    return await update(category.categoryId, category)
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
      schema: AdminCategoryListView,
    })
    if (result.kind === 'unreadableAnswer') {
      await load()
      return adminFailureFrom(result)
    }
    if (result.kind !== 'ok') {
      return adminFailureFrom(result)
    }
    if (categoriesGate.isNewestRequest(token)) {
      categories.value = result.data.categories
      return adminOk(null)
    }
    await load()
    return adminOk(null)
  }

  function listen(): () => void {
    return useConnectionStore().listenToTheLaptop(['ConfigurationChanged'], load)
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
