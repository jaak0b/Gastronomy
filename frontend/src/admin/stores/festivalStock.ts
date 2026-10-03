import { defineStore } from 'pinia'
import { ref } from 'vue'
import { requestAction } from '../../shared/api/client'
import {
  AdminFestivalIngredientListView,
  type AdminFestivalIngredientView,
} from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { reloadOrFailureOf } from '../core/adminMutation'
import { loadAdminList } from '../core/listLoading'
import { createLatestRequestGate } from '../../shared/core/latestRequestGate'
import { useConnectionStore } from '../../shared/stores/connection'

export const useAdminFestivalStockStore = defineStore('adminFestivalStock', () => {
  const ingredients = ref<AdminFestivalIngredientView[]>([])
  const loadFailed = ref(false)
  const festivalInView = ref<string | null>(null)

  const stockGate = createLatestRequestGate()

  async function loadForFestival(festivalId: string): Promise<void> {
    if (festivalInView.value !== festivalId) {
      ingredients.value = []
    }
    festivalInView.value = festivalId
    await loadAdminList({
      path: `/api/admin/festivals/${festivalId}/ingredients`,
      schema: AdminFestivalIngredientListView,
      gate: stockGate,
      itemsOf: (response) => response.ingredients,
      showItems: (loaded) => {
        ingredients.value = loaded
      },
      setLoadFailed: (failed) => {
        loadFailed.value = failed
      },
    })
  }

  async function reload(): Promise<void> {
    const festivalId = festivalInView.value
    if (festivalId !== null) {
      await loadForFestival(festivalId)
    }
  }

  async function setAvailableAmount(
    festivalId: string,
    ingredientId: string,
    availableAmount: number | null,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/ingredients/${ingredientId}`, {
        method: 'PUT',
        body: { availableAmount },
      }),
      reload,
    )
  }

  function listen(): () => void {
    return useConnectionStore().listenToTheLaptop(['ConfigurationChanged', 'OrdersChanged'], reload)
  }

  return {
    ingredients,
    loadFailed,
    loadForFestival,
    setAvailableAmount,
    listen,
  }
})
