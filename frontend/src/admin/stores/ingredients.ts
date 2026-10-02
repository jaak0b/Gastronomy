import { defineStore } from 'pinia'
import { ref } from 'vue'
import { request, requestAction } from '../../shared/api/client'
import {
  AdminIngredientListView,
  AdminIngredientView,
  type IngredientUnit,
} from '../../shared/api/generatedSchemas'
import { adminOk, type AdminActionResult } from '../core/adminActionResult'
import { adminFailureFrom, reloadOrFailureOf } from '../core/adminMutation'
import { loadAdminList } from '../core/adminList'
import { createPendingCreatedEntities } from '../core/pendingCreatedEntities'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'

export interface IngredientDraft {
  name: string
  unit: IngredientUnit
}

export const useAdminIngredientsStore = defineStore('adminIngredients', () => {
  const ingredients = ref<AdminIngredientView[]>([])
  const loadFailed = ref(false)

  const ingredientsGate = createLatestRequestGate()
  const pendingCreatedIngredients = createPendingCreatedEntities<AdminIngredientView>(
    (ingredient) => ingredient.ingredientId,
  )

  async function load(): Promise<void> {
    await loadAdminList({
      path: '/api/admin/ingredients',
      schema: AdminIngredientListView,
      gate: ingredientsGate,
      itemsOf: (response) => response.ingredients,
      showItems: (loaded) => {
        ingredients.value = pendingCreatedIngredients.mergeInto(loaded)
      },
      setLoadFailed: (failed) => {
        loadFailed.value = failed
      },
    })
  }

  async function create(draft: IngredientDraft): Promise<AdminActionResult<AdminIngredientView>> {
    const result = await request('/api/admin/ingredients', {
      method: 'POST',
      body: { name: draft.name, unit: draft.unit },
      schema: AdminIngredientView,
    })
    if (result.kind !== 'ok') {
      return adminFailureFrom(result)
    }
    pendingCreatedIngredients.remember(result.data)
    ingredients.value = pendingCreatedIngredients.mergeInto(ingredients.value)
    return adminOk(result.data)
  }

  async function save(
    ingredientId: string,
    draft: IngredientDraft,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/ingredients/${ingredientId}`, {
        method: 'PUT',
        body: { name: draft.name, unit: draft.unit },
      }),
      load,
    )
  }

  async function setActive(
    ingredientId: string,
    isActive: boolean,
  ): Promise<AdminActionResult<null>> {
    const action = isActive ? 'activate' : 'deactivate'
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/ingredients/${ingredientId}/${action}`, { method: 'POST' }),
      load,
    )
  }

  function findIngredientWithId(ingredientId: string): AdminIngredientView | null {
    return (
      ingredients.value.find((ingredient) => ingredient.ingredientId === ingredientId) ?? null
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
    ingredients,
    loadFailed,
    load,
    create,
    save,
    setActive,
    findIngredientWithId,
    listen,
  }
})
