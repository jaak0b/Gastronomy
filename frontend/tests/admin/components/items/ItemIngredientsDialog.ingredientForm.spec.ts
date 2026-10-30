import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { answer, refusal } from '../../../support/laptop'
import { anAdminIngredient } from '../../../support/wireViews'
import {
  allOnScreen,
  clickOn,
  inputOf,
  isOnScreen,
  onScreen,
  textOnScreen,
  typeIn,
  waitUntil,
} from '../../../support/dom'
import { formDialogHolding, saveTheIngredientDialog } from '../../../support/formDialogs'
import { chosenUnitIds } from '../ingredients/ingredientDialogFixture'
import {
  FLOUR_LINE,
  PANCAKE,
  ingredientRow,
  loadIngredientsAndMountDialog,
  namesInTheIngredientList,
  recipeLaptop,
  searchInput,
} from './itemIngredientsDialogFixture'

const NAME_FIELD = '[data-test="ingredient-name-field"]'

const RENAMED_MILK = anAdminIngredient({
  ingredientId: 'ingredient-milch',
  name: 'Vollmilch',
  unitId: 'unit-millilitre',
  unitName: 'ml',
})

async function openTheFormThrough(selector: string): Promise<void> {
  await clickOn(selector)
  await vi.waitFor(() => expect(isOnScreen(NAME_FIELD)).toBe(true))
}

function titleOfTheForm(): string {
  return onScreen('[data-test="form-dialog-title"]', formDialogHolding(NAME_FIELD)).textContent ?? ''
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the pencil on an ingredient', () => {
  it('opens the form of that ingredient with the name it already has', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    await openTheFormThrough(`${ingredientRow('ingredient-milch')} [data-test="edit-ingredient"]`)

    expect(titleOfTheForm()).toBe('Zutat bearbeiten')
    expect(inputOf(NAME_FIELD).value).toBe('Milch')
  })

  it('sends the changed name and unit for that ingredient', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    await openTheFormThrough(`${ingredientRow('ingredient-milch')} [data-test="edit-ingredient"]`)

    await saveTheIngredientDialog('Vollmilch', 'unit-litre')

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        { url: '/api/admin/ingredients/ingredient-milch', method: 'PUT', body: { name: 'Vollmilch', unitId: 'unit-litre' } },
      ]),
    )
  })

  it('shows the new name in the list of available ingredients once the list is read again', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    await openTheFormThrough(`${ingredientRow('ingredient-milch')} [data-test="edit-ingredient"]`)
    laptop.answers('GET', '/api/admin/ingredients', answer({ ingredients: [RENAMED_MILK] }))

    await saveTheIngredientDialog('Vollmilch', 'unit-millilitre')

    await waitUntil(() => expect(textOnScreen(`${ingredientRow('ingredient-milch')} [data-test="ingredient-name"]`)).toBe('Vollmilch'))
  })
})

describe('the pencil on a recipe line', () => {
  it('shows the amount, the pencil and the remove action in this order', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    const actions = allOnScreen(
      `${FLOUR_LINE} [data-test="amount-input"], ${FLOUR_LINE} [data-test="edit-recipe-ingredient"], ${FLOUR_LINE} [data-test="remove-recipe-line"]`,
    )

    expect(actions.map((action) => action.dataset.test)).toEqual([
      'amount-input',
      'edit-recipe-ingredient',
      'remove-recipe-line',
    ])
  })

  it('opens the form of that ingredient with its name and unit', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    await openTheFormThrough(`${FLOUR_LINE} [data-test="edit-recipe-ingredient"]`)

    expect(titleOfTheForm()).toBe('Zutat bearbeiten')
    expect(inputOf(NAME_FIELD).value).toBe('Mehl')
    expect(chosenUnitIds()).toEqual(['unit-gram'])
  })

  it('shows the changed name on the recipe line once the item is reloaded', async () => {
    const laptop = recipeLaptop()
    const dialog = await loadIngredientsAndMountDialog()
    await openTheFormThrough(`${FLOUR_LINE} [data-test="edit-recipe-ingredient"]`)

    await saveTheIngredientDialog('Weizenmehl', 'unit-gram')

    await waitUntil(() =>
      expect(laptop.writes()).toEqual([
        {
          url: '/api/admin/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { name: 'Weizenmehl', unitId: 'unit-gram' },
        },
      ]),
    )
    const reloadedItem = {
      ...PANCAKE,
      ingredients: PANCAKE.ingredients.map((line) =>
        line.ingredientId === 'ingredient-mehl' ? { ...line, name: 'Weizenmehl' } : line,
      ),
    }
    await dialog.setProps({ item: reloadedItem })

    expect(textOnScreen(`${FLOUR_LINE} [data-test="recipe-ingredient-name"]`)).toBe('Weizenmehl')
  })
})

describe('the button for a new ingredient', () => {
  it.each([
    ['de', 'Neue Zutat'],
    ['en', 'New ingredient'],
  ] as const)('is named in %s under the list of available ingredients', async (locale, expected) => {
    recipeLaptop()

    await loadIngredientsAndMountDialog(locale)

    expect(onScreen('[data-test="ingredients-column"] [data-test="new-ingredient"]').textContent?.trim()).toBe(expected)
  })

  it('opens an empty form headed as a new ingredient', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    await openTheFormThrough('[data-test="new-ingredient"]')

    expect(titleOfTheForm()).toBe('Neue Zutat')
    expect(inputOf(NAME_FIELD).value).toBe('')
  })

  it('starts the name with the search text exactly as typed when the search shows no ingredient', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()
    await typeIn(searchInput(), 'bio-Eier ')

    await openTheFormThrough('[data-test="new-ingredient"]')

    expect(inputOf(NAME_FIELD).value).toBe('bio-Eier ')
  })

  it('starts with an empty name while the search still shows an ingredient', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()
    await typeIn(searchInput(), 'Mil')

    await openTheFormThrough('[data-test="new-ingredient"]')

    expect(inputOf(NAME_FIELD).value).toBe('')
  })
})

describe('a new ingredient once the laptop has saved it', () => {
  it('is sent with the typed name and the chosen unit', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    await openTheFormThrough('[data-test="new-ingredient"]')

    await saveTheIngredientDialog('Ei', 'unit-stueck')

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        { url: '/api/admin/ingredients', method: 'POST', body: { name: 'Ei', unitId: 'unit-stueck' } },
      ]),
    )
  })

  it('shows its row in the available list and closes the form', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()
    await openTheFormThrough('[data-test="new-ingredient"]')

    await saveTheIngredientDialog('Ei', 'unit-stueck')

    await waitUntil(() => expect(namesInTheIngredientList()).toContain('Ei'))
    expect(isOnScreen(NAME_FIELD)).toBe(false)
  })

  it('clears the search that would hide its row', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()
    await typeIn(searchInput(), 'Hühnerei')
    await openTheFormThrough('[data-test="new-ingredient"]')

    await saveTheIngredientDialog('Ei', 'unit-stueck')

    await waitUntil(() => expect(namesInTheIngredientList()).toContain('Ei'))
    expect(searchInput().value).toBe('')
  })

  it('keeps the search that shows its row', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()
    await typeIn(searchInput(), 'Ei')
    await openTheFormThrough('[data-test="new-ingredient"]')

    await saveTheIngredientDialog('Ei', 'unit-stueck')

    await waitUntil(() => expect(namesInTheIngredientList()).toContain('Ei'))
    expect(searchInput().value).toBe('Ei')
  })

  it('stays closed when the laptop refuses the name, which the form names', async () => {
    recipeLaptop().answers(
      'POST',
      '/api/admin/ingredients',
      refusal('errors.admin.ingredients.nameTaken', { status: 400, code: 'ValidationFailed' }),
    )
    await loadIngredientsAndMountDialog()
    await openTheFormThrough('[data-test="new-ingredient"]')

    await saveTheIngredientDialog('milch', 'unit-millilitre')

    await vi.waitFor(() =>
      expect(onScreen('[data-test="refusal"]', formDialogHolding(NAME_FIELD)).textContent?.trim()).toBe(
        'Eine Zutat mit diesem Namen ist bereits vorhanden. Wählen Sie einen anderen Namen.',
      ),
    )
    expect(isOnScreen(NAME_FIELD)).toBe(true)
  })
})
