import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request, requestAction } from '../../shared/api/client'
import { AdminCategoryListView, AdminCategoryView } from '../../shared/api/generatedSchemas'
import { adminOk, type AdminActionResult } from '../core/adminActionResult'
import { adminFailureFrom, reloadOrFailureOf } from '../core/adminMutation'
import { loadAdminList } from '../core/adminList'
import { createPendingCreatedEntities } from '../core/pendingCreatedEntities'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'

export interface AdminCategoryDraft {
  name: string
  colourHex: string
}

export type CategoryMoveDirection = 'up' | 'down'

export const useAdminCategoriesStore = defineStore('adminCategories', () => {
  const categories = ref<AdminCategoryView[]>([])
  const loadFailed = ref(false)

  const categoriesGate = createLatestRequestGate()
  const pendingCreatedCategories = createPendingCreatedEntities<AdminCategoryView>(
    (category) => category.categoryId,
  )
  let latestMove: Promise<AdminActionResult<null>> = Promise.resolve(adminOk(null))

  async function load(): Promise<void> {
    await loadAdminList({
      path: '/api/admin/categories',
      schema: AdminCategoryListView,
      gate: categoriesGate,
      itemsOf: (response) => response.categories,
      showItems: (loaded) => {
        categories.value = pendingCreatedCategories.mergeInto(loaded)
      },
      setLoadFailed: (failed) => {
        loadFailed.value = failed
      },
    })
  }

  async function create(draft: AdminCategoryDraft): Promise<AdminActionResult<AdminCategoryView>> {
    const result = await request('/api/admin/categories', {
      method: 'POST',
      body: { name: draft.name, colourHex: draft.colourHex },
      schema: AdminCategoryView,
    })
    if (result.kind !== 'ok') {
      return adminFailureFrom(result)
    }
    pendingCreatedCategories.remember(result.data)
    categories.value = pendingCreatedCategories.mergeInto(categories.value)
    return adminOk(result.data)
  }

  async function save(
    category: AdminCategoryDraft & { categoryId: string },
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/categories/${category.categoryId}`, {
        method: 'PUT',
        body: { name: category.name, colourHex: category.colourHex },
      }),
      load,
    )
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

  async function setActive(
    categoryId: string,
    isActive: boolean,
  ): Promise<AdminActionResult<null>> {
    const action = isActive ? 'activate' : 'deactivate'
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/categories/${categoryId}/${action}`, { method: 'POST' }),
      load,
    )
  }

  function listen(): () => void {
    const connection = useConnectionStore()
    const releases = [
      connection.registerRefetch(load),
      connection.onEvent<unknown>('ConfigurationChanged', () => {
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
