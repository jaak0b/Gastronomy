import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { VAutocomplete } from 'vuetify/components'
import ItemsList from '../../../../src/admin/components/items/ItemsList.vue'
import { testPlugins } from '../../../support/plugins'
import { answer, type StubbedLaptop } from '../../../support/laptop'
import { inputOf, onScreen, textOnScreen, typeInto } from '../../../support/dom'
import { anAdminIngredient } from '../../../support/wireViews'
import { mountList, itemsListLaptop, ITEM_ID, ONE_ITEM } from './itemsListFixture'

const MUSTARD = anAdminIngredient({ ingredientId: 'ingredient-senf', name: 'Senf', unit: 'gram' })

const BRATWURST_WITH_MUSTARD = {
  items: [{ ...ONE_ITEM.items[0], ingredients: [{ ingredientId: 'ingredient-senf', amount: 20 }] }],
}

const MUSTARD_LINE = '[data-test="recipe-line"][data-test-id="ingredient-senf"]'

function laptopThatKeepsTheAddedLine(): StubbedLaptop {
  const laptop = itemsListLaptop({ ingredients: { ingredients: [MUSTARD] } })
  return laptop.answers(
    'GET',
    (call) => call.url === '/api/admin/items',
    (call) => answer(laptop.callsTo('PUT', /\/ingredients\//).length === 0 ? ONE_ITEM : BRATWURST_WITH_MUSTARD)(call),
  )
}

async function openTheRecipe(list: VueWrapper): Promise<void> {
  await vi.waitFor(() => expect(list.find('[data-test="edit-ingredients"]').exists()).toBe(true))
  await list.get('[data-test="edit-ingredients"]').trigger('click')
  await vi.waitFor(() => expect(document.querySelector('[data-test="ingredient-search"]')).not.toBeNull())
}

beforeEach(() => {
  setActivePinia(createPinia())
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
  it('sends the amount and shows the ingredient as a line of the recipe', async () => {
    const laptop = laptopThatKeepsTheAddedLine()
    const list = mountList()
    await openTheRecipe(list)
    await list.getComponent<typeof VAutocomplete>('[data-test="ingredient-search"]').setValue('ingredient-senf')
    await vi.waitFor(() =>
      expect(inputOf('[data-test="add-recipe-line"] [data-test="amount-input"]').disabled).toBe(false),
    )

    typeInto('[data-test="add-recipe-line"] [data-test="amount-input"]', '20')
    await vi.waitFor(() => expect(onScreen('[data-test="add-to-recipe"]').hasAttribute('disabled')).toBe(false))
    onScreen('[data-test="add-to-recipe"]').click()

    await vi.waitFor(() => expect(textOnScreen(`${MUSTARD_LINE} [data-test="recipe-ingredient-name"]`)).toBe('Senf'))
    expect(laptop.writes()).toEqual([
      { url: `/api/admin/items/${ITEM_ID}/ingredients/ingredient-senf`, method: 'PUT', body: { amount: 20 } },
    ])
  })
})
