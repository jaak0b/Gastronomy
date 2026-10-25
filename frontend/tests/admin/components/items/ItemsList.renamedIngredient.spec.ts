import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../../support/hubConnection'
import { answer } from '../../../support/laptop'
import { textOnScreen, waitUntil } from '../../../support/dom'
import { anAdminItemIngredient } from '../../../support/wireViews'

vi.mock('@microsoft/signalr', async () => (await import('../../../support/hubConnection')).signalrModuleFake())

const { useConnectionStore } = await import('../../../../src/shared/stores/connection')
const { itemsListLaptop, mountList, openTheRecipe, ONE_ITEM } = await import('./itemsListFixture')

const MUSTARD_NAME = '[data-test="recipe-line"][data-test-id="ingredient-senf"] [data-test="recipe-ingredient-name"]'

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
          amount: 20,
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
          amount: 20,
        }),
      ],
    },
  ],
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

describe('an ingredient renamed while the recipe of an article is open', () => {
  it('shows the new name in the open recipe once the laptop says the configuration changed', async () => {
    const laptop = itemsListLaptop({ items: BRATWURST_WITH_MUSTARD })
    await useConnectionStore().connect({})
    const list = mountList()
    await openTheRecipe(list)
    await waitUntil(() => expect(textOnScreen(MUSTARD_NAME)).toBe('Senf'))
    laptop.answers('GET', (call) => call.url === '/api/admin/items', answer(BRATWURST_WITH_DIJON_MUSTARD))

    fireHubEvent('ConfigurationChanged')

    await waitUntil(() => expect(textOnScreen(MUSTARD_NAME)).toBe('Dijonsenf'))
  })
})
