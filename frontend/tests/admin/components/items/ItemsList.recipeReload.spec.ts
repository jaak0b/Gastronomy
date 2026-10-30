import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../../support/hubConnection'
import { answer } from '../../../support/laptop'
import { clickOn, onScreen, pressInDialog, typeInto, waitUntil } from '../../../support/dom'
import { anAdminIngredient, anAdminItemIngredient } from '../../../support/wireViews'

vi.mock('@microsoft/signalr', async () => (await import('../../../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../../../src/shared/stores/connection')
const { itemsListLaptop, mountList, openTheRecipe, ONE_ITEM } = await import('./itemsListFixture')

const UNUSED_MUSTARD = anAdminIngredient({
  ingredientId: 'ingredient-senf',
  name: 'Senf',
  unitId: 'unit-gram',
  unitName: 'g',
})

const MUSTARD_IN_BRATWURST = { ...UNUSED_MUSTARD, usedByItemNames: ['Bratwurst'] }

const BRATWURST_WITH_MUSTARD = {
  items: [
    {
      ...ONE_ITEM.items[0],
      ingredients: [
        anAdminItemIngredient({ ingredientId: 'ingredient-senf', name: 'Senf', unitId: 'unit-gram', unitName: 'g', amount: 20 }),
      ],
    },
  ],
}

const MUSTARD_ROW = '[data-test="ingredient-row"][data-test-id="ingredient-senf"]'

function trashOfMustardIsDisabled(): boolean {
  return (onScreen(`${MUSTARD_ROW} [data-test="delete-ingredient"]`) as HTMLButtonElement).disabled
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

describe('the trash of an ingredient once the laptop says the configuration changed', () => {
  it('is disabled after the ingredient was added to the open recipe', async () => {
    const laptop = itemsListLaptop({ ingredients: { ingredients: [UNUSED_MUSTARD] } })
    await useConnectionStore().connect({})
    const list = mountList()
    await openTheRecipe(list)
    await waitUntil(() => expect(trashOfMustardIsDisabled()).toBe(false))
    laptop
      .answers('GET', (call) => call.url === '/api/admin/items', answer(BRATWURST_WITH_MUSTARD))
      .answers('GET', '/api/admin/ingredients', answer({ ingredients: [MUSTARD_IN_BRATWURST] }))
    await clickOn(`${MUSTARD_ROW} [data-test="add-ingredient"]`)
    typeInto(`${MUSTARD_ROW} [data-test="amount-input"]`, '20')
    await waitUntil(() => expect(onScreen('[data-test="add-to-recipe"]').hasAttribute('disabled')).toBe(false))
    await clickOn('[data-test="add-to-recipe"]')

    fireHubEvent('ConfigurationChanged')

    await waitUntil(() => expect(trashOfMustardIsDisabled()).toBe(true))
  })

  it('is enabled again after the ingredient was removed from the open recipe', async () => {
    const laptop = itemsListLaptop({ items: BRATWURST_WITH_MUSTARD, ingredients: { ingredients: [MUSTARD_IN_BRATWURST] } })
    await useConnectionStore().connect({})
    const list = mountList()
    await openTheRecipe(list)
    await waitUntil(() => expect(trashOfMustardIsDisabled()).toBe(true))
    laptop
      .answers('GET', (call) => call.url === '/api/admin/items', answer(ONE_ITEM))
      .answers('GET', '/api/admin/ingredients', answer({ ingredients: [UNUSED_MUSTARD] }))
    await clickOn('[data-test="recipe-line"][data-test-id="ingredient-senf"] [data-test="remove-recipe-line"]')
    await pressInDialog('[data-test="confirm"]')

    fireHubEvent('ConfigurationChanged')

    await waitUntil(() => expect(trashOfMustardIsDisabled()).toBe(false))
  })
})
