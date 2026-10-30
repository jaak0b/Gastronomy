import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, type VueWrapper } from '@vue/test-utils'
import { refusal } from '../../../support/laptop'
import { allOnScreen, inputOf, isOnScreen, onScreen, typeIn, typeInto, waitUntil } from '../../../support/dom'
import { formDialogHolding, saveTheIngredientDialog } from '../../../support/formDialogs'
import {
  addButtonIsDisabled,
  addedAmountInput,
  chooseIngredient,
  ingredientSearch,
  loadIngredientsAndMountDialog,
  recipeLaptop,
  recipeLaptopWithNothingLeftToAdd,
} from './itemIngredientsDialogFixture'

const OFFERED_CHOICE = '.v-autocomplete__content .v-list-item-title'

const ADD_ROW_REFUSAL = '[data-test="add-recipe-line"] [data-test="refusal"]'

async function createIngredientThroughTheForm(name: string, unitId: string): Promise<void> {
  onScreen('[data-test="new-ingredient"]').click()
  await vi.waitFor(() => expect(isOnScreen('[data-test="ingredient-name-field"]')).toBe(true))
  await saveTheIngredientDialog(name, unitId)
}

async function addTheChosenIngredientWith(amount: string): Promise<void> {
  typeInto(addedAmountInput(), amount)
  await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
  onScreen('[data-test="add-to-recipe"]').click()
}

async function openTheIngredientSearch(): Promise<void> {
  onScreen('[data-test="ingredient-search"] .v-field').dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
  await waitUntil(() => expect(isOnScreen(OFFERED_CHOICE)).toBe(true))
}

async function dialogAfterTheLaptopRefusedAddingMilk(): Promise<VueWrapper> {
  recipeLaptop().answers(
    'PUT',
    /./,
    refusal('errors.admin.ingredients.amountInvalid', { status: 422, code: 'UnprocessableEntity' }),
  )
  const dialog = await loadIngredientsAndMountDialog()
  await chooseIngredient(dialog, 'ingredient-milch')
  await addTheChosenIngredientWith('250')
  await waitUntil(() => expect(isOnScreen(ADD_ROW_REFUSAL)).toBe(true))
  return dialog
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('choosing an ingredient to add', () => {
  it('offers only the active ingredients that are not yet in the recipe', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    await openTheIngredientSearch()

    expect(allOnScreen(OFFERED_CHOICE).map((choice) => choice.textContent)).toEqual(['Milch', 'Sahne'])
  })

  it.each([
    ['de', 'Alle Zutaten sind bereits hinzugefügt. Klicken Sie auf „Neue Zutat“.'],
    ['en', 'Every ingredient has already been added. Click "New ingredient".'],
  ] as const)('points in %s to the new ingredient button when nothing is left to choose', async (locale, expected) => {
    recipeLaptopWithNothingLeftToAdd()
    await loadIngredientsAndMountDialog(locale)

    await openTheIngredientSearch()

    expect(allOnScreen(OFFERED_CHOICE).map((choice) => choice.textContent)).toEqual([expected])
  })

  it('keeps the amount field switched off and shows no unit until an ingredient is chosen', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(addedAmountInput().disabled).toBe(true)
    expect(allOnScreen('[data-test="add-recipe-line"] [data-test="amount-unit"]')).toEqual([])
  })

  it('keeps the add button disabled until an ingredient is chosen', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(addButtonIsDisabled()).toBe(true)
  })

  it('shows the unit of the chosen ingredient beside the amount', async () => {
    recipeLaptop()
    const dialog = await loadIngredientsAndMountDialog()

    await chooseIngredient(dialog, 'ingredient-milch')

    expect(onScreen('[data-test="add-recipe-line"] [data-test="amount-unit"]').textContent).toBe('ml')
  })
})

describe('adding the chosen ingredient', () => {
  it.each([
    ['de', 'Zutat hinzufügen'],
    ['en', 'Add the ingredient'],
  ] as const)('names the add button in %s', async (locale, expected) => {
    recipeLaptop()

    await loadIngredientsAndMountDialog(locale)

    expect(onScreen('[data-test="add-to-recipe"]').textContent?.trim()).toBe(expected)
  })

  it('sends one request for the amount', async () => {
    const laptop = recipeLaptop()
    const dialog = await loadIngredientsAndMountDialog()
    await chooseIngredient(dialog, 'ingredient-milch')

    await addTheChosenIngredientWith('250')

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        {
          url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-milch',
          method: 'PUT',
          body: { amount: 250 },
        },
      ]),
    )
  })

  it('empties the add row after a successful add', async () => {
    recipeLaptop()
    const dialog = await loadIngredientsAndMountDialog()
    await chooseIngredient(dialog, 'ingredient-milch')

    await addTheChosenIngredientWith('250')

    await vi.waitFor(() => expect(ingredientSearch(dialog).props('modelValue')).toBeNull())
    expect(addedAmountInput().value).toBe('')
    expect(addedAmountInput().disabled).toBe(true)
  })

  it.each([
    ['no amount', ''],
    ['zero', '0'],
    ['unreadable text', 'viel'],
  ])('keeps the add button disabled with %s', async (_description, amount) => {
    recipeLaptop()
    const dialog = await loadIngredientsAndMountDialog()
    await chooseIngredient(dialog, 'ingredient-milch')

    typeInto(addedAmountInput(), amount)
    await flushPromises()

    expect(addButtonIsDisabled()).toBe(true)
  })

  it('shows under the add row why the laptop refused the amount', async () => {
    recipeLaptop().answers(
      'PUT',
      /./,
      refusal('errors.admin.ingredients.amountInvalid', { status: 422, code: 'UnprocessableEntity' }),
    )
    const dialog = await loadIngredientsAndMountDialog()
    await chooseIngredient(dialog, 'ingredient-milch')

    await addTheChosenIngredientWith('250')

    await vi.waitFor(() =>
      expect(onScreen('[data-test="add-recipe-line"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Geben Sie eine Menge größer als null an.',
      ),
    )
    expect(allOnScreen('[data-test="recipe-line"] [data-test="refusal"]')).toEqual([])
  })

  it('keeps the chosen ingredient and the typed amount when the laptop refuses the amount', async () => {
    recipeLaptop().answers(
      'PUT',
      /./,
      refusal('errors.admin.ingredients.amountInvalid', { status: 422, code: 'UnprocessableEntity' }),
    )
    const dialog = await loadIngredientsAndMountDialog()
    await chooseIngredient(dialog, 'ingredient-milch')

    await addTheChosenIngredientWith('250')

    await vi.waitFor(() => expect(isOnScreen('[data-test="add-recipe-line"] [data-test="refusal"]')).toBe(true))
    expect(ingredientSearch(dialog).props('modelValue')).toBe('ingredient-milch')
    expect(addedAmountInput().value).toBe('250')
  })
})

describe('creating an ingredient from the recipe', () => {
  it('opens the empty form of the ingredients tab', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    onScreen('[data-test="new-ingredient"]').click()

    await vi.waitFor(() => expect(isOnScreen('[data-test="ingredient-name-field"]')).toBe(true))
    const form = formDialogHolding('[data-test="ingredient-name-field"]')
    expect(onScreen('[data-test="form-dialog-title"]', form).textContent).toBe('Neue Zutat')
    expect(inputOf('[data-test="ingredient-name-field"]').value).toBe('')
  })

  it('sends the new ingredient, closes the form and chooses the ingredient for adding', async () => {
    const laptop = recipeLaptop()
    const dialog = await loadIngredientsAndMountDialog()

    await createIngredientThroughTheForm('Ei', 'unit-stueck')

    await vi.waitFor(() => expect(isOnScreen('[data-test="ingredient-name-field"]')).toBe(false))
    expect(laptop.writes()).toEqual([
      { url: '/api/admin/ingredients', method: 'POST', body: { name: 'Ei', unitId: 'unit-stueck' } },
    ])
    expect(ingredientSearch(dialog).props('modelValue')).toBe('ingredient-ei')
  })

  it('adds the created ingredient with the amount the admin types afterwards', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    await createIngredientThroughTheForm('Ei', 'unit-stueck')
    await vi.waitFor(() => expect(addedAmountInput().disabled).toBe(false))

    await addTheChosenIngredientWith('3')

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/ingredients',
        'PUT /api/admin/items/item-pfannkuchen/ingredients/ingredient-ei',
      ]),
    )
  })

  it.each([
    ['errors.admin.ingredients.nameMissing', {}, 'Geben Sie einen Namen für die Zutat an.'],
    ['errors.admin.ingredients.nameTaken', {}, 'Eine Zutat mit diesem Namen ist bereits vorhanden. Wählen Sie einen anderen Namen.'],
    [
      'errors.admin.ingredients.nameBelongsToDeactivatedIngredient',
      { name: 'Honig' },
      'Die Zutat Honig ist bereits vorhanden, aber deaktiviert. Aktivieren Sie sie unter „Zutaten“.',
    ],
  ])('shows the refusal %s inside the form and chooses nothing', async (messageKey, parameters, expected) => {
    recipeLaptop().answers(
      'POST',
      '/api/admin/ingredients',
      refusal(messageKey, { status: 400, code: 'ValidationFailed', parameters }),
    )
    const dialog = await loadIngredientsAndMountDialog()

    await createIngredientThroughTheForm('honig', 'unit-gram')

    const form = formDialogHolding('[data-test="ingredient-name-field"]')
    await vi.waitFor(() => expect(onScreen('[data-test="refusal"]', form).textContent?.trim()).toBe(expected))
    expect(ingredientSearch(dialog).props('modelValue')).toBeNull()
  })
})

describe('a refusal under the add row', () => {
  it('goes away as soon as the admin chooses another ingredient', async () => {
    const dialog = await dialogAfterTheLaptopRefusedAddingMilk()

    await chooseIngredient(dialog, 'ingredient-sahne')

    await waitUntil(() => expect(isOnScreen(ADD_ROW_REFUSAL)).toBe(false))
  })

  it('goes away as soon as the admin types another amount', async () => {
    await dialogAfterTheLaptopRefusedAddingMilk()

    await typeIn(addedAmountInput(), '300')

    await waitUntil(() => expect(isOnScreen(ADD_ROW_REFUSAL)).toBe(false))
  })
})
