import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { flushPromises } from '@vue/test-utils'
import { aHold, answer, emptyAnswer, heldUntil, refusal } from '../../../support/laptop'
import { clickOn, inputOf, isOnScreen, leave, onScreen, typeIn, waitUntil } from '../../../support/dom'
import { anAdminItemIngredient } from '../../../support/wireViews'
import {
  FLOUR_LINE,
  PANCAKE,
  ingredientRow,
  loadIngredientsAndMountDialog,
  namesInTheIngredientList,
  recipeLaptop,
} from './itemIngredientsDialogFixture'

const MILK_ROW = ingredientRow('ingredient-milch')

const MILK_LINE = '[data-test="recipe-line"][data-test-id="ingredient-milch"]'

const MILK_REFUSAL = `${MILK_ROW} [data-test="refusal"]`

const MILK_PUT = /\/ingredients\/ingredient-milch$/

const PANCAKE_WITH_MILK = {
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
  ],
}

function thePlusOf(ingredientId: string): string {
  return `${ingredientRow(ingredientId)} [data-test="add-ingredient"]`
}

async function addMilk(): Promise<void> {
  await clickOn(thePlusOf('ingredient-milch'))
}

async function showTheReloadedItem(dialog: Awaited<ReturnType<typeof loadIngredientsAndMountDialog>>): Promise<void> {
  await dialog.setProps({ item: PANCAKE_WITH_MILK })
  await waitUntil(() => expect(isOnScreen(MILK_LINE)).toBe(true))
}

function theMilkAmountInput(): HTMLInputElement {
  return inputOf(`${MILK_LINE} [data-test="amount-input"]`)
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the plus on an available ingredient', () => {
  it('assigns the ingredient with amount 1 in exactly one request', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()

    await addMilk()

    await waitUntil(() =>
      expect(laptop.writes()).toEqual([
        {
          url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-milch',
          method: 'PUT',
          body: { amount: 1 },
        },
      ]),
    )
  })

  it('shows the new line with 1 and focuses its amount with the text selected', async () => {
    recipeLaptop().answers('GET', (call) => call.url === '/api/admin/items', answer({ items: [PANCAKE_WITH_MILK] }))
    const dialog = await loadIngredientsAndMountDialog()
    await addMilk()

    await showTheReloadedItem(dialog)

    const amount = theMilkAmountInput()
    expect(amount.value).toBe('1')
    expect(document.activeElement).toBe(amount)
    expect(amount.selectionStart).toBe(0)
    expect(amount.selectionEnd).toBe(1)
  })

  it('takes the assigned ingredient out of the available column once the item is reloaded', async () => {
    recipeLaptop().answers('GET', (call) => call.url === '/api/admin/items', answer({ items: [PANCAKE_WITH_MILK] }))
    const dialog = await loadIngredientsAndMountDialog()
    await addMilk()

    await showTheReloadedItem(dialog)

    expect(isOnScreen(MILK_ROW)).toBe(false)
    expect(namesInTheIngredientList()).toEqual(['Sahne'])
  })

  it('sends the amount typed over the focused 1', async () => {
    const laptop = recipeLaptop().answers(
      'GET',
      (call) => call.url === '/api/admin/items',
      answer({ items: [PANCAKE_WITH_MILK] }),
    )
    const dialog = await loadIngredientsAndMountDialog()
    await addMilk()
    await showTheReloadedItem(dialog)
    const amount = theMilkAmountInput()

    await typeIn(amount, '150')
    leave(amount)

    await waitUntil(() => expect(laptop.bodiesSentTo('PUT', MILK_PUT)).toEqual([{ amount: 1 }, { amount: 150 }]))
  })

  it('disables the plus while its assignment is pending and sends only once', async () => {
    const hold = aHold()
    const laptop = recipeLaptop().answers('PUT', MILK_PUT, heldUntil(hold.released, emptyAnswer(204)))
    await loadIngredientsAndMountDialog()

    onScreen(thePlusOf('ingredient-milch')).click()
    await flushPromises()
    expect((onScreen(thePlusOf('ingredient-milch')) as HTMLButtonElement).disabled).toBe(true)
    onScreen(thePlusOf('ingredient-milch')).click()
    await flushPromises()

    expect(laptop.callsTo('PUT', MILK_PUT)).toHaveLength(1)
    hold.release()
    await waitUntil(() => expect((onScreen(thePlusOf('ingredient-milch')) as HTMLButtonElement).disabled).toBe(false))
  })

  it('shows the refusal under that row and offers the plus again', async () => {
    const laptop = recipeLaptop().answers(
      'PUT',
      MILK_PUT,
      refusal('errors.admin.ingredients.amountInvalid', { status: 422, code: 'UnprocessableEntity' }),
    )
    await loadIngredientsAndMountDialog()

    await addMilk()

    await waitUntil(() =>
      expect(onScreen(MILK_REFUSAL).textContent?.trim()).toBe('Geben Sie eine Menge größer als null an.'),
    )
    expect((onScreen(thePlusOf('ingredient-milch')) as HTMLButtonElement).disabled).toBe(false)
    laptop.answers('PUT', MILK_PUT, emptyAnswer(204))
    await addMilk()

    await waitUntil(() => expect(laptop.callsTo('PUT', MILK_PUT)).toHaveLength(2))
    expect(isOnScreen(MILK_REFUSAL)).toBe(false)
  })
})

describe('a recipe amount the admin clears', () => {
  it('sets a cleared amount back to 1 and sends 1', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    const flourAmount = inputOf(`${FLOUR_LINE} [data-test="amount-input"]`)

    await typeIn(flourAmount, '')
    leave(flourAmount)

    await waitUntil(() => expect(laptop.bodiesSentTo('PUT', /ingredient-mehl$/)).toEqual([{ amount: 1 }]))
    expect(flourAmount.value).toBe('1')
  })

  it('restores a cleared amount that is already 1 without sending', async () => {
    const laptop = recipeLaptop()
    const dialog = await loadIngredientsAndMountDialog()
    const itemWithOne = {
      ...PANCAKE,
      ingredients: PANCAKE.ingredients.map((line) =>
        line.ingredientId === 'ingredient-mehl' ? { ...line, amount: 1 } : line,
      ),
    }
    await dialog.setProps({ item: itemWithOne })
    const flourAmount = inputOf(`${FLOUR_LINE} [data-test="amount-input"]`)

    await typeIn(flourAmount, '')
    leave(flourAmount)
    await flushPromises()

    expect(flourAmount.value).toBe('1')
    expect(laptop.writes()).toEqual([])
  })
})
