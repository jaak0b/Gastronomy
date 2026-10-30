import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { fireHubEvent, forgetHubEvents } from '../../../support/hubConnection'
import { answer } from '../../../support/laptop'
import { clickOn, isOnScreen, onScreen, pressInDialog, waitUntil } from '../../../support/dom'
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

describe('the available ingredients once the laptop says the configuration changed', () => {
  it('takes an ingredient the item now holds out of the available list', async () => {
    const laptop = itemsListLaptop({ ingredients: { ingredients: [UNUSED_MUSTARD] } })
    await useConnectionStore().connect({})
    const list = mountList()
    await openTheRecipe(list)
    await waitUntil(() => expect(isOnScreen(MUSTARD_ROW)).toBe(true))
    laptop
      .answers('GET', (call) => call.url === '/api/admin/items', answer(BRATWURST_WITH_MUSTARD))
      .answers('GET', '/api/admin/ingredients', answer({ ingredients: [MUSTARD_IN_BRATWURST] }))

    fireHubEvent('ConfigurationChanged')

    await waitUntil(() => expect(isOnScreen(MUSTARD_ROW)).toBe(false))
  })

  it('shows an ingredient the item no longer holds with its trash enabled once the laptop lists it as unused', async () => {
    const laptop = itemsListLaptop({
      items: BRATWURST_WITH_MUSTARD,
      ingredients: { ingredients: [MUSTARD_IN_BRATWURST] },
    })
    await useConnectionStore().connect({})
    const list = mountList()
    await openTheRecipe(list)
    expect(isOnScreen(MUSTARD_ROW)).toBe(false)
    laptop
      .answers('GET', (call) => call.url === '/api/admin/items', answer(ONE_ITEM))
      .answers('GET', '/api/admin/ingredients', answer({ ingredients: [UNUSED_MUSTARD] }))
    await clickOn('[data-test="recipe-line"][data-test-id="ingredient-senf"] [data-test="remove-recipe-line"]')
    await pressInDialog('[data-test="confirm"]')

    fireHubEvent('ConfigurationChanged')

    await waitUntil(() => expect(trashOfMustardIsDisabled()).toBe(false))
    expect(isOnScreen(MUSTARD_ROW)).toBe(true)
  })
})
