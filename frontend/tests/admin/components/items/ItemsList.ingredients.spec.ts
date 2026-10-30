import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ItemsList from '../../../../src/admin/components/items/ItemsList.vue'
import { testPlugins } from '../../../support/plugins'
import { fireHubEvent, forgetHubEvents } from '../../../support/hubConnection'
import { answer, emptyAnswer, type StubbedLaptop } from '../../../support/laptop'
import {
  allOnScreen,
  clickOn,
  inputOf,
  isOnScreen,
  pressInDialog,
  textOnScreen,
  waitForDialog,
  waitUntil,
} from '../../../support/dom'
import { anAdminIngredient, anAdminItemIngredient } from '../../../support/wireViews'
import { saveTheIngredientDialog } from '../../../support/formDialogs'
import { mountList, itemsListLaptop, openTheRecipe, ITEM_ID, ONE_ITEM } from './itemsListFixture'

vi.mock('@microsoft/signalr', async () => (await import('../../../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../../../src/shared/stores/connection')

const MUSTARD = anAdminIngredient({
  ingredientId: 'ingredient-senf',
  name: 'Senf',
  unitId: 'unit-gram',
  unitName: 'g',
})

const BRATWURST_WITH_MUSTARD = {
  items: [
    {
      ...ONE_ITEM.items[0],
      ingredients: [
        anAdminItemIngredient({
          ingredientId: 'ingredient-senf',
          name: 'Senf',
          unitId: 'unit-gram',
          unitName: 'g',
          amount: 1,
        }),
      ],
    },
  ],
}

const BRATWURST_WITH_DIJON_MUSTARD = {
  items: [
    {
      ...ONE_ITEM.items[0],
      ingredients: [
        anAdminItemIngredient({
          ingredientId: 'ingredient-senf',
          name: 'Dijonsenf',
          unitId: 'unit-gram',
          unitName: 'g',
          amount: 1,
        }),
      ],
    },
  ],
}

const MUSTARD_LINE = '[data-test="recipe-line"][data-test-id="ingredient-senf"]'

const MUSTARD_ROW = '[data-test="ingredient-row"][data-test-id="ingredient-senf"]'

function laptopThatKeepsTheAddedLine(): StubbedLaptop {
  const laptop = itemsListLaptop({ ingredients: { ingredients: [MUSTARD] } })
  return laptop.answers(
    'GET',
    (call) => call.url === '/api/admin/items',
    (call) => answer(laptop.callsTo('PUT', /\/ingredients\//).length === 0 ? ONE_ITEM : BRATWURST_WITH_MUSTARD)(call),
  )
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

describe('the ingredients button on an article', () => {
  it('opens the recipe of that article in German', async () => {
    itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="edit-ingredients"]').exists()).toBe(true))
    expect(list.get('[data-test="edit-ingredients"]').text()).toBe('Zutaten bearbeiten')
    await list.get('[data-test="edit-ingredients"]').trigger('click')

    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog-title"]')?.textContent).toBe(
        'Zutaten für Bratwurst',
      ),
    )
  })

  it('is worded in English', async () => {
    itemsListLaptop()

    const list = mount(ItemsList, {
      global: { plugins: testPlugins('en') },
      attachTo: document.body,
    })
    await vi.waitFor(() => expect(list.find('[data-test="edit-ingredients"]').exists()).toBe(true))

    expect(list.get('[data-test="edit-ingredients"]').text()).toBe('Edit ingredients')
  })

  it('closes the recipe when the admin presses close', async () => {
    itemsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="edit-ingredients"]').exists()).toBe(true))
    await list.get('[data-test="edit-ingredients"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-cancel"]')).not.toBeNull())
    const close = document.querySelector('[data-test="form-cancel"]') as HTMLElement

    expect(close.textContent?.trim()).toBe('Schließen')
    close.click()

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
  })
})

describe('adding an ingredient to the recipe of an article', () => {
  it('assigns it with amount 1, shows it as a line and takes it out of the available list', async () => {
    const laptop = laptopThatKeepsTheAddedLine()
    const list = mountList()
    await openTheRecipe(list)
    await vi.waitFor(() => expect(isOnScreen(`${MUSTARD_ROW} [data-test="add-ingredient"]`)).toBe(true))
    await clickOn(`${MUSTARD_ROW} [data-test="add-ingredient"]`)

    await vi.waitFor(() => expect(textOnScreen(`${MUSTARD_LINE} [data-test="recipe-ingredient-name"]`)).toBe('Senf'))
    expect(laptop.writes()).toEqual([
      { url: `/api/admin/items/${ITEM_ID}/ingredients/ingredient-senf`, method: 'PUT', body: { amount: 1 } },
    ])
    const amount = inputOf(`${MUSTARD_LINE} [data-test="amount-input"]`)
    expect(amount.value).toBe('1')
    expect(document.activeElement).toBe(amount)
    expect(amount.selectionStart).toBe(0)
    expect(amount.selectionEnd).toBe(1)
    expect(isOnScreen(MUSTARD_ROW)).toBe(false)
  })
})

describe('editing an ingredient from a recipe line on the Artikel tab', () => {
  it('updates the recipe line name after saving the ingredient', async () => {
    const laptop = itemsListLaptop({
      items: BRATWURST_WITH_MUSTARD,
      ingredients: { ingredients: [MUSTARD] },
    })
    laptop.answers('PUT', '/api/admin/ingredients/ingredient-senf', emptyAnswer(204))
    await useConnectionStore().connect({})
    const list = mountList()
    await openTheRecipe(list)
    await waitUntil(() => expect(isOnScreen(`${MUSTARD_LINE} [data-test="edit-recipe-ingredient"]`)).toBe(true))
    await clickOn(`${MUSTARD_LINE} [data-test="edit-recipe-ingredient"]`)
    await waitUntil(() => expect(isOnScreen('[data-test="ingredient-name-field"]')).toBe(true))
    expect(inputOf('[data-test="ingredient-name-field"]').value).toBe('Senf')

    laptop
      .answers('GET', (call) => call.url === '/api/admin/items', answer(BRATWURST_WITH_DIJON_MUSTARD))
      .answers('GET', '/api/admin/ingredients', answer({ ingredients: [{ ...MUSTARD, name: 'Dijonsenf' }] }))
    await saveTheIngredientDialog('Dijonsenf', 'unit-gram')

    await waitUntil(() =>
      expect(laptop.writes()).toContainEqual({
        url: '/api/admin/ingredients/ingredient-senf',
        method: 'PUT',
        body: { name: 'Dijonsenf', unitId: 'unit-gram' },
      }),
    )
    fireHubEvent('ConfigurationChanged')

    await waitUntil(() =>
      expect(textOnScreen(`${MUSTARD_LINE} [data-test="recipe-ingredient-name"]`)).toBe('Dijonsenf'),
    )
  })
})

describe('deleting a unit from the ingredient form on the Artikel tab', () => {
  it('deletes the unit once the recipe popup and the ingredient form are open', async () => {
    const laptop = itemsListLaptop({ ingredients: { ingredients: [MUSTARD] } })
    laptop.answers('DELETE', '/api/admin/units/unit-gram', emptyAnswer(204))
    const list = mountList()
    await openTheRecipe(list)
    await vi.waitFor(() => expect(isOnScreen(`${MUSTARD_ROW} [data-test="edit-ingredient"]`)).toBe(true))
    await clickOn(`${MUSTARD_ROW} [data-test="edit-ingredient"]`)
    await vi.waitFor(() => expect(isOnScreen('[data-test="ingredient-name-field"]')).toBe(true))

    await clickOn('[data-test="unit-chip"][data-test-id="unit-gram"] [data-test="delete-unit"]')
    await waitForDialog()
    await pressInDialog('[data-test="confirm"]')

    await waitUntil(() =>
      expect(laptop.writes()).toContainEqual({ url: '/api/admin/units/unit-gram', method: 'DELETE', body: undefined }),
    )
  })
})

describe('the text of an empty recipe', () => {
  it('is the shared text of an empty list in a recipe without a line', async () => {
    itemsListLaptop({ items: ONE_ITEM })

    const list = mountList()
    await openTheRecipe(list)

    expect(
      allOnScreen('[data-test="recipe-column"] [data-test="empty-list-text"]').map((text) => text.textContent?.trim()),
    ).toEqual(['Keine Einträge'])
  })

  it('stays away in a recipe with a line', async () => {
    itemsListLaptop({ items: BRATWURST_WITH_MUSTARD })

    const list = mountList()
    await openTheRecipe(list)

    expect(allOnScreen('[data-test="recipe-column"] [data-test="empty-list-text"]')).toHaveLength(0)
  })
})
