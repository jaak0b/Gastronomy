import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../../support/hubConnection'
import { answer } from '../../../support/laptop'
import type { AdminItemIngredientView } from '../../../../src/shared/api/generatedSchemas'
import { anAdminItemIngredient } from '../../../support/wireViews'

vi.mock('@microsoft/signalr', async () => (await import('../../../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../../../src/shared/stores/connection')
const { DEACTIVATED_ITEM, ONE_ITEM, itemsListLaptop, mountList } = await import('./itemsListFixture')

const BREAD_ROLL = anAdminItemIngredient({ ingredientId: 'ingredient-broetchen', name: 'Brötchen' })
const MUSTARD = anAdminItemIngredient({ ingredientId: 'ingredient-senf', name: 'Senf', unitId: 'unit-gram', unitName: 'g', amount: 20 })
const ONION = anAdminItemIngredient({ ingredientId: 'ingredient-zwiebel', name: 'Zwiebel', unitId: 'unit-gram', unitName: 'g', amount: 30 })

function bratwurstWith(ingredients: AdminItemIngredientView[]) {
  return { items: [{ ...ONE_ITEM.items[0], ingredients }] }
}

beforeEach(() => {
  setActivePinia(createPinia())
  forgetHubEvents()
  localStorage.clear()
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the ingredient count on an item row', () => {
  it.each([
    { ingredients: [], shown: 'Keine Zutaten' },
    { ingredients: [BREAD_ROLL], shown: '1 Zutat' },
    { ingredients: [BREAD_ROLL, MUSTARD, ONION], shown: '3 Zutaten' },
  ])('reads "$shown" in German for a recipe of $ingredients.length lines', async ({ ingredients, shown }) => {
    itemsListLaptop({ items: bratwurstWith(ingredients) })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))

    expect(list.get('[data-test="item-row"] [data-test="ingredient-count"]').text()).toBe(shown)
  })

  it.each([
    { ingredients: [], shown: 'No ingredients' },
    { ingredients: [BREAD_ROLL], shown: '1 ingredient' },
    { ingredients: [BREAD_ROLL, MUSTARD, ONION], shown: '3 ingredients' },
  ])('reads "$shown" in English for a recipe of $ingredients.length lines', async ({ ingredients, shown }) => {
    itemsListLaptop({ items: bratwurstWith(ingredients) })

    const list = mountList('en')
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))

    expect(list.get('[data-test="item-row"] [data-test="ingredient-count"]').text()).toBe(shown)
  })

  it('stands directly before "Zutaten bearbeiten" on the row', async () => {
    itemsListLaptop({ items: bratwurstWith([BREAD_ROLL]) })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))

    const parts = list
      .get('[data-test="item-row"] [data-test="row-line"]')
      .findAll('[data-test="ingredient-count"], [data-test="edit-ingredients"], [data-test="edit-item"]')
    expect(parts.map((part) => part.attributes('data-test'))).toEqual([
      'ingredient-count',
      'edit-ingredients',
      'edit-item',
    ])
  })

  it('is shown on a deactivated item once deactivated items are shown', async () => {
    itemsListLaptop({ items: { items: [{ ...DEACTIVATED_ITEM.items[0], ingredients: [BREAD_ROLL, MUSTARD] }] } })
    const list = mountList()

    await list.get('[data-test="show-deactivated"] input').setValue(true)

    await vi.waitFor(() =>
      expect(list.get('[data-test="item-row"] [data-test="ingredient-count"]').text()).toBe('2 Zutaten'),
    )
  })
})

describe('the ingredient count once the laptop says the configuration changed', () => {
  it('counts the recipe line the item now holds', async () => {
    const laptop = itemsListLaptop({ items: bratwurstWith([BREAD_ROLL]) })
    await useConnectionStore().connect({})
    const list = mountList()
    await vi.waitFor(() =>
      expect(list.get('[data-test="item-row"] [data-test="ingredient-count"]').text()).toBe('1 Zutat'),
    )
    laptop.answers('GET', (call) => call.url === '/api/admin/items', answer(bratwurstWith([BREAD_ROLL, MUSTARD])))

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() =>
      expect(list.get('[data-test="item-row"] [data-test="ingredient-count"]').text()).toBe('2 Zutaten'),
    )
  })
})
