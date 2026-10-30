import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminIngredientsStore } from '../../../../src/admin/stores/ingredients'
import { answer, neverAnswers } from '../../../support/laptop'
import { allOnScreen, iconActionLabelsOnScreen, isOnScreen, onScreen, typeIn } from '../../../support/dom'
import {
  hover,
  ingredientIdsOfRowsHolding,
  ingredientRow,
  loadIngredientsAndMountDialog,
  mountDialog,
  namesInTheIngredientList,
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
    ['de', ['Im Rezept', 'Alle Zutaten']],
    ['en', ['In the recipe', 'All ingredients']],
  ] as const)('are headed in %s', async (locale, expected) => {
    recipeLaptop()

    await loadIngredientsAndMountDialog(locale)

    expect(allOnScreen('[data-test="column-heading"]').map((heading) => heading.textContent?.trim())).toEqual(expected)
  })

  it('keep the recipe on the left and every ingredient on the right', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(allOnScreen('[data-test="recipe-column"] [data-test="recipe-line"]')).toHaveLength(2)
    expect(allOnScreen('[data-test="ingredients-column"] [data-test="ingredient-row"]')).toHaveLength(4)
  })
})

describe('the list of all ingredients', () => {
  it('lists every ingredient, those in the recipe included, with its unit after a middle dot', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(namesInTheIngredientList()).toEqual(['Brötchen', 'Mehl', 'Milch', 'Sahne'])
    expect(allOnScreen('[data-test="ingredient-unit"]').map((unit) => unit.textContent?.trim())).toEqual([
      '· Stück',
      '· g',
      '· ml',
      '· ml',
    ])
  })

  it('stripes its rows like every admin list', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(onScreen(ingredientRow('ingredient-broetchen')).classList.contains('striped-row-strong')).toBe(false)
    expect(onScreen(ingredientRow('ingredient-mehl')).classList.contains('striped-row-strong')).toBe(true)
  })

  it('offers the plus only on an ingredient that is not yet in the recipe', async () => {
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

describe('searching the list of all ingredients', () => {
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

    await typeIn(searchInput(), 'ö')

    expect(namesInTheIngredientList()).toEqual(['Brötchen'])
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

describe('the text of an empty list of all ingredients', () => {
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
