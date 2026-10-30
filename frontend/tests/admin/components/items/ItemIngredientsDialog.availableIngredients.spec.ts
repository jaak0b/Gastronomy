import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminIngredientsStore } from '../../../../src/admin/stores/ingredients'
import { answer, neverAnswers } from '../../../support/laptop'
import { allOnScreen, iconActionLabelsOnScreen, isOnScreen, onScreen, typeIn, waitUntil } from '../../../support/dom'
import { anAdminItemIngredient } from '../../../support/wireViews'
import {
  hover,
  ingredientIdsOfRowsHolding,
  ingredientRow,
  loadIngredientsAndMountDialog,
  mountDialog,
  namesInTheIngredientList,
  PANCAKE,
  recipeLaptop,
  recipeLaptopWithoutTheIngredientList,
  searchInput,
} from './itemIngredientsDialogFixture'

const EMPTY_TEXT_OF_THE_INGREDIENTS = '[data-test="ingredients-column"] [data-test="empty-list-text"]'

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the two columns of the recipe', () => {
  it.each([
    ['de', ['Im Rezept', 'Verfügbare Zutaten']],
    ['en', ['In the recipe', 'Available ingredients']],
  ] as const)('are headed in %s', async (locale, expected) => {
    recipeLaptop()

    await loadIngredientsAndMountDialog(locale)

    expect(allOnScreen('[data-test="column-heading"]').map((heading) => heading.textContent?.trim())).toEqual(expected)
  })

  it('keep the recipe on the left and the ingredients it does not hold on the right', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(allOnScreen('[data-test="recipe-column"] [data-test="recipe-line"]')).toHaveLength(2)
    expect(allOnScreen('[data-test="ingredients-column"] [data-test="ingredient-row"]')).toHaveLength(2)
  })
})

describe('the list of available ingredients', () => {
  it('lists only the ingredients that are not in the recipe, with their unit after a middle dot', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(namesInTheIngredientList()).toEqual(['Milch', 'Sahne'])
    expect(allOnScreen('[data-test="ingredient-unit"]').map((unit) => unit.textContent?.trim())).toEqual([
      '· ml',
      '· ml',
    ])
  })

  it('stripes its rows like every admin list', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(onScreen(ingredientRow('ingredient-milch')).classList.contains('striped-row-strong')).toBe(false)
    expect(onScreen(ingredientRow('ingredient-sahne')).classList.contains('striped-row-strong')).toBe(true)
  })

  it('offers the plus on every listed ingredient', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(ingredientIdsOfRowsHolding('[data-test="add-ingredient"]')).toEqual(['ingredient-milch', 'ingredient-sahne'])
  })

  it('offers the pencil, the trash and the plus in this order', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    const actions = allOnScreen(
      `${ingredientRow('ingredient-milch')} [data-test="edit-ingredient"], ${ingredientRow('ingredient-milch')} [data-test="delete-ingredient"], ${ingredientRow('ingredient-milch')} [data-test="add-ingredient"]`,
    )
    expect(actions.map((action) => action.dataset.test)).toEqual(['edit-ingredient', 'delete-ingredient', 'add-ingredient'])
  })

  it.each([
    ['de', 'Zutat hinzufügen'],
    ['en', 'Add the ingredient'],
  ] as const)('names the plus in %s', async (locale, expected) => {
    recipeLaptop()
    await loadIngredientsAndMountDialog(locale)

    await hover(`${ingredientRow('ingredient-milch')} [data-test="add-ingredient"]`)

    await vi.waitFor(() => expect(iconActionLabelsOnScreen()).toContain(expected))
  })
})

describe('searching the list of available ingredients', () => {
  it.each([
    ['de', 'Suchen'],
    ['en', 'Search'],
  ] as const)('is labelled in %s', async (locale, expected) => {
    recipeLaptop()

    await loadIngredientsAndMountDialog(locale)

    expect(onScreen('[data-test="ingredient-search"] label').textContent).toContain(expected)
  })

  it('keeps the ingredients whose name holds the typed text, whatever its case', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    await typeIn(searchInput(), 'LCH')

    expect(namesInTheIngredientList()).toEqual(['Milch'])
  })

  it('finds the typed text anywhere in the name', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    await typeIn(searchInput(), 'ah')

    expect(namesInTheIngredientList()).toEqual(['Sahne'])
  })

  it('leaves the recipe on the left as it is', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    await typeIn(searchInput(), 'Sahne')

    expect(allOnScreen('[data-test="recipe-line"]')).toHaveLength(2)
  })

  it('shows no text of an empty list when the search matches no ingredient', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    await typeIn(searchInput(), 'Zimt')

    expect(namesInTheIngredientList()).toEqual([])
    expect(isOnScreen(EMPTY_TEXT_OF_THE_INGREDIENTS)).toBe(false)
  })
})

describe('the text of an empty list of available ingredients', () => {
  it('stays away when the recipe already holds every ingredient', async () => {
    recipeLaptop()
    const dialog = await loadIngredientsAndMountDialog()
    await dialog.setProps({
      item: {
        ...PANCAKE,
        ingredients: [
          ...PANCAKE.ingredients,
          anAdminItemIngredient({
            ingredientId: 'ingredient-milch',
            name: 'Milch',
            unitId: 'unit-millilitre',
            unitName: 'ml',
            amount: 1,
          }),
          anAdminItemIngredient({
            ingredientId: 'ingredient-sahne',
            name: 'Sahne',
            unitId: 'unit-millilitre',
            unitName: 'ml',
            amount: 1,
          }),
        ],
      },
    })

    await waitUntil(() =>
      expect(allOnScreen('[data-test="ingredients-column"] [data-test="ingredient-row"]')).toHaveLength(0),
    )
    expect(isOnScreen(EMPTY_TEXT_OF_THE_INGREDIENTS)).toBe(false)
  })

  it('shows when the laptop has no ingredient at all', async () => {
    recipeLaptop().answers('GET', '/api/admin/ingredients', answer({ ingredients: [] }))

    await loadIngredientsAndMountDialog()

    expect(onScreen(EMPTY_TEXT_OF_THE_INGREDIENTS).textContent?.trim()).toBe('Keine Einträge')
  })

  it('stays away before the laptop has answered', async () => {
    recipeLaptop().answers('GET', '/api/admin/ingredients', neverAnswers())
    void useAdminIngredientsStore().load()

    mountDialog()
    await flushPromises()

    expect(isOnScreen(EMPTY_TEXT_OF_THE_INGREDIENTS)).toBe(false)
  })

  it('stays away after the load failed', async () => {
    recipeLaptopWithoutTheIngredientList()
    await useAdminIngredientsStore().load()

    mountDialog()
    await flushPromises()

    expect(isOnScreen(EMPTY_TEXT_OF_THE_INGREDIENTS)).toBe(false)
  })
})
