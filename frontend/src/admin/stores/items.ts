import { defineStore } from 'pinia'
import { requestAction } from '../../shared/api/client'
import { AdminItemListView, AdminItemView } from '../../shared/api/generatedSchemas'
import type { AdminActionResult } from '../core/adminActionResult'
import { reloadOrFailureOf } from '../core/adminMutation'
import { useConnectionStore } from '../../shared/stores/connection'
import { defineFestivalScopedList } from './festivalScopedList'

export interface AdminItemDraft {
  itemId?: string
  name: string
  categoryId: string
  sortOrder: number
  productionMinutes: number | null
  isQueueIndependent: boolean
}

export interface FestivalPlacement {
  priceCents: number
  stationIds: string[]
}

export const useAdminItemsStore = defineStore('adminItems', () => {
  const {
    entries: items,
    loadFailed,
    load,
    loadAtTheFestival,
    reload,
    create,
    save,
    setActive,
  } = defineFestivalScopedList({
    path: '/api/admin/items',
    listSchema: AdminItemListView,
    entrySchema: AdminItemView,
    entriesOf: (response) => response.items,
    idOf: (item) => item.itemId,
    draftIdOf: (item: AdminItemDraft) => item.itemId,
    requestBodyOf: (item) => ({
      name: item.name.trim(),
      categoryId: item.categoryId,
      sortOrder: item.sortOrder,
      productionMinutes: item.productionMinutes,
      isQueueIndependent: item.isQueueIndependent,
    }),
  })

  async function putAtTheFestival(
    festivalId: string,
    itemId: string,
    placement: FestivalPlacement,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/items/${itemId}`, {
        method: 'PUT',
        body: { priceCents: placement.priceCents, stationIds: placement.stationIds },
      }),
      reload,
    )
  }

  async function removeFromTheFestival(
    festivalId: string,
    itemId: string,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/items/${itemId}`, {
        method: 'DELETE',
      }),
      reload,
    )
  }

  async function setAvailability(
    festivalId: string,
    itemId: string,
    isAvailable: boolean,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/festivals/${festivalId}/items/${itemId}/availability`, {
        method: 'POST',
        body: { isAvailable },
      }),
      reload,
    )
  }

  async function setIngredientAmount(
    itemId: string,
    ingredientId: string,
    amount: number,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/items/${itemId}/ingredients/${ingredientId}`, {
        method: 'PUT',
        body: { amount },
      }),
      reload,
    )
  }

  async function removeIngredient(
    itemId: string,
    ingredientId: string,
  ): Promise<AdminActionResult<null>> {
    return await reloadOrFailureOf(
      await requestAction(`/api/admin/items/${itemId}/ingredients/${ingredientId}`, {
        method: 'DELETE',
      }),
      reload,
    )
  }

  function listen(): () => void {
    return useConnectionStore().listenToTheLaptop(['ConfigurationChanged'], reload)
  }

  return {
    items,
    loadFailed,
    load,
    loadAtTheFestival,
    create,
    save,
    putAtTheFestival,
    removeFromTheFestival,
    setAvailability,
    setActive,
    setIngredientAmount,
    removeIngredient,
    listen,
  }
})
