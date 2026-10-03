import { defineStore } from 'pinia'
import {
  AdminIngredientListView,
  AdminIngredientView,
  type IngredientUnit,
  type SaveIngredientRequest,
} from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { useConnectionStore } from '../../shared/stores/connection'
import { defineAdminList } from './adminList'

export interface IngredientDraft {
  name: string
  unit: IngredientUnit
}

export const useAdminIngredientsStore = defineStore('adminIngredients', () => {
  const {
    entries: ingredients,
    loadFailed,
    load,
    create,
    update,
    setActive,
  } = defineAdminList({
    path: '/api/admin/ingredients',
    listSchema: AdminIngredientListView,
    entrySchema: AdminIngredientView,
    entriesOf: (response) => response.ingredients,
    idOf: (ingredient) => ingredient.ingredientId,
    requestBodyOf: (draft: IngredientDraft): SaveIngredientRequest => ({ name: draft.name, unit: draft.unit }),
  })

  async function save(
    ingredientId: string,
    draft: IngredientDraft,
  ): Promise<AdminActionResult<null>> {
    return await update(ingredientId, draft)
  }

  function findIngredientWithId(ingredientId: string): AdminIngredientView | null {
    return (
      ingredients.value.find((ingredient) => ingredient.ingredientId === ingredientId) ?? null
    )
  }

  function listen(): () => void {
    return useConnectionStore().listenToTheLaptop(['ConfigurationChanged'], load)
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
