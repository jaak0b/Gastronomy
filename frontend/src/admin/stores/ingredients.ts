import { defineStore } from 'pinia'
import {
  AdminIngredientListView,
  AdminIngredientView,
  type SaveIngredientRequest,
} from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { useAdminLoadStateStore } from './adminLoadState'
import { adminListDefinition, defineAdminList, withActivation, withCreating } from './adminList'

export interface IngredientDraft {
  name: string
  unitId: string
}

export const useAdminIngredientsStore = defineStore('adminIngredients', () => {
  const definition = adminListDefinition({
    path: '/api/admin/ingredients',
    listSchema: AdminIngredientListView,
    entrySchema: AdminIngredientView,
    entriesOf: (response) => response.ingredients,
    idOf: (ingredient) => ingredient.ingredientId,
    requestBodyOf: (draft: IngredientDraft): SaveIngredientRequest => ({
      name: draft.name,
      unitId: draft.unitId,
    }),
  })
  const {
    entries: ingredients,
    loadFailed,
    load,
    create,
    update,
    setActive,
  } = withActivation(withCreating(defineAdminList(definition), definition), definition)

  async function save(
    ingredientId: string,
    draft: IngredientDraft,
  ): Promise<AdminActionResult<null>> {
    return await update(ingredientId, draft)
  }

  function listen(): () => void {
    return useAdminLoadStateStore().listenToTheLaptop(['ConfigurationChanged'], load, loadFailed)
  }

  return {
    ingredients,
    loadFailed,
    load,
    create,
    save,
    setActive,
    listen,
  }
})
