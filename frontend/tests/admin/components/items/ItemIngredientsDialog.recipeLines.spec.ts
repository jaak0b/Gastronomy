import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { refusal } from '../../../support/laptop'
import {
  allOnScreen,
  dialogText,
  inputOf,
  isOnScreen,
  leave,
  onScreen,
  openDialog,
  pressInDialog,
  typeIn,
  typeInto,
  waitForDialog,
  waitUntil,
} from '../../../support/dom'
import { BUN_LINE, FLOUR_LINE, loadIngredientsAndMountDialog, recipeLaptop } from './itemIngredientsDialogFixture'

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the recipe lines of an article', () => {
  it('show each ingredient with its amount and its unit in German', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Zutaten für Pfannkuchen')
    expect(allOnScreen('[data-test="recipe-ingredient-name"]').map((name) => name.textContent)).toEqual([
      'Mehl',
      'Brötchen',
    ])
    const amounts = allOnScreen('[data-test="recipe-line"] [data-test="amount-input"] input') as HTMLInputElement[]
    expect(amounts.map((input) => input.value)).toEqual(['1500', '2'])
    expect(allOnScreen('[data-test="recipe-line"] [data-test="amount-unit"]').map((unit) => unit.textContent)).toEqual([
      'g',
      'Stück',
    ])
  })

  it('show the title and the units in English', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog('en')

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Ingredients for Pfannkuchen')
    expect(allOnScreen('[data-test="recipe-line"] [data-test="amount-unit"]').map((unit) => unit.textContent)).toEqual([
      'g',
      'pieces',
    ])
  })

  it('sit in a dialog wider than an ordinary form', async () => {
    recipeLaptop()

    await loadIngredientsAndMountDialog()

    expect(onScreen('[data-test="form-dialog"]').closest<HTMLElement>('.v-overlay__content')?.style.maxWidth).toBe('880px')
  })

  it('save a changed decimal amount as soon as the admin leaves the field', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    const flourAmount = inputOf(`${FLOUR_LINE} [data-test="amount-input"]`)

    typeInto(flourAmount, '2250,5')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        {
          url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { amount: 2250.5 },
        },
      ]),
    )
  })
})

describe('a recipe line whose amount is refused', () => {
  it('shows under that line that the amount is not a number, sends nothing and keeps what was typed', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    const flourAmount = inputOf(`${FLOUR_LINE} [data-test="amount-input"]`)

    await typeIn(flourAmount, 'viel')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(onScreen(`${FLOUR_LINE} [data-test="refusal"]`).textContent?.trim()).toBe(
        'Geben Sie eine Menge größer als null an.',
      ),
    )
    expect(laptop.writes()).toEqual([])
    expect(flourAmount.value).toBe('viel')
  })

  it('shows the reason the laptop gives in English under that line only', async () => {
    recipeLaptop().answers(
      'PUT',
      /./,
      refusal('errors.admin.ingredients.amountInvalid', { status: 422, code: 'UnprocessableEntity' }),
    )
    await loadIngredientsAndMountDialog('en')
    const flourAmount = inputOf(`${FLOUR_LINE} [data-test="amount-input"]`)

    typeInto(flourAmount, '0')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(onScreen(`${FLOUR_LINE} [data-test="refusal"]`).textContent?.trim()).toBe('Enter an amount above zero.'),
    )
    expect(allOnScreen('[data-test="refusal"]')).toHaveLength(1)
  })

  it('takes the warning away as soon as the admin types the saved amount again', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()
    const flourAmount = inputOf(`${FLOUR_LINE} [data-test="amount-input"]`)
    await typeIn(flourAmount, 'viel')
    leave(flourAmount)
    await waitUntil(() => expect(isOnScreen(`${FLOUR_LINE} [data-test="refusal"]`)).toBe(true))

    await typeIn(flourAmount, '1500')

    await waitUntil(() => expect(isOnScreen(`${FLOUR_LINE} [data-test="refusal"]`)).toBe(false))
  })
})

describe('removing an ingredient from the recipe', () => {
  it('asks first in German, naming the article, and sends nothing yet', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()

    onScreen(`${BUN_LINE} [data-test="remove-recipe-line"]`).click()

    await waitForDialog()
    expect(dialogText('[data-test="confirm-title"]')).toBe('Zutat entfernen')
    expect(dialogText('[data-test="confirm-body"]').trim()).toBe('Die Zutat wird nur bei Pfannkuchen entfernt.')
    expect(dialogText('[data-test="confirm"]').trim()).toBe('Entfernen')
    expect(laptop.writes()).toEqual([])
  })

  it('asks in English', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog('en')

    onScreen(`${BUN_LINE} [data-test="remove-recipe-line"]`).click()

    await waitForDialog()
    expect(dialogText('[data-test="confirm-title"]')).toBe('Remove the ingredient')
    expect(dialogText('[data-test="confirm-body"]').trim()).toBe('The ingredient is removed from Pfannkuchen only.')
    expect(dialogText('[data-test="confirm"]').trim()).toBe('Remove')
  })

  it('removes the ingredient once the admin confirms', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    onScreen(`${BUN_LINE} [data-test="remove-recipe-line"]`).click()

    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'DELETE /api/admin/items/item-pfannkuchen/ingredients/ingredient-broetchen',
      ]),
    )
  })

  it('keeps the ingredient when the admin cancels the question', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    onScreen(`${BUN_LINE} [data-test="remove-recipe-line"]`).click()

    await pressInDialog('[data-test="cancel"]')

    await vi.waitFor(() => expect(openDialog()).toBeNull())
    expect(laptop.writes()).toEqual([])
  })

  it('shows under that line why the laptop refused the removal', async () => {
    recipeLaptop().answers('DELETE', /./, refusal('errors.admin.actionFailed', { status: 409 }))
    await loadIngredientsAndMountDialog()
    onScreen(`${BUN_LINE} [data-test="remove-recipe-line"]`).click()

    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() => expect(isOnScreen(`${BUN_LINE} [data-test="refusal"]`)).toBe(true))
    expect(onScreen(`${BUN_LINE} [data-test="refusal"]`).textContent?.trim()).toBe(
      'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
    )
  })
})
