import { mount, type VueWrapper } from '@vue/test-utils'
import ItemDialog from '../../../../src/admin/components/items/ItemDialog.vue'
import { AdminCategoryView, AdminItemView } from '../../../../src/shared/api/generatedSchemas'
import { useAdminCategoriesStore } from '../../../../src/admin/stores/categories'
import { testPlugins } from '../../../support/plugins'
import { inputOf } from '../../../support/dom'
import { stubLaptop, answer, type StubbedLaptop } from '../../../support/laptop'
import { nextTick } from 'vue'

export const FOOD_ID = '33333333-3333-3333-3333-333333333333'

export const DRINKS_ID = '44444444-4444-4444-4444-444444444444'

export const DESSERT_ID = '55555555-5555-5555-5555-555555555555'

export const CATEGORIES: AdminCategoryView[] = [
  { categoryId: FOOD_ID, name: 'Speisen', colourHex: '#FFEB3B', sortOrder: 1, isActive: true },
  { categoryId: DRINKS_ID, name: 'Getränke', colourHex: '#C62828', sortOrder: 2, isActive: true },
]

export const CREATED_CATEGORY: AdminCategoryView = {
  categoryId: DESSERT_ID,
  name: 'Nachtisch',
  colourHex: '#6D4C41',
  sortOrder: 3,
  isActive: true,
}

export const BRATWURST: AdminItemView = {
  itemId: 'item-1',
  name: 'Bratwurst',
  categoryId: FOOD_ID,
  sortOrder: 1,
  isActive: true,
  productionMinutes: 15,
  isQueueIndependent: false,
  atTheFestival: { priceCents: 350, isAvailable: true, stationIds: ['station-kueche'] },
}

export function mountDialog(item: AdminItemView | null = null, locale: 'de' | 'en' = 'de'): VueWrapper {
  return mount(ItemDialog, {
    props: { item, errorText: null },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

export async function pressSave(dialog: VueWrapper): Promise<void> {
  ;(document.querySelector('[data-test="form-save"]') as HTMLElement).click()
  await nextTick()
}

export function pressEnterInTheMinutes(dialog: VueWrapper): void {
  inputOf('[data-test="production-minutes-field"]').dispatchEvent(
    new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }),
  )
}

export function knownCategories(): void {
  useAdminCategoriesStore().categories = [...CATEGORIES]
}

export function categoriesLaptop(): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(answer({ categories: [...CATEGORIES, CREATED_CATEGORY] }))
    .answers('POST', (call) => call.url === '/api/admin/categories', answer(CREATED_CATEGORY, 201))
}
