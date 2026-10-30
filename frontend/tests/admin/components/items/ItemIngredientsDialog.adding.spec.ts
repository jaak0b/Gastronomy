import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { flushPromises } from '@vue/test-utils'
import { refusal } from '../../../support/laptop'
import { allOnScreen, isOnScreen, onScreen, textOnScreen, typeIn, typeInto, waitUntil } from '../../../support/dom'
import {
  AMOUNT_TO_ADD,
  addButtonIsDisabled,
  amountToAddInput,
  ingredientIdsOfRowsHolding,
  ingredientRow,
  loadIngredientsAndMountDialog,
  openTheAmountRowOf,
  recipeLaptop,
} from './itemIngredientsDialogFixture'

const MILK_ROW = ingredientRow('ingredient-milch')

const MILK_REFUSAL = `${MILK_ROW} [data-test="refusal"]`

async function typeTheAmountToAdd(amount: string): Promise<void> {
  typeInto(amountToAddInput(), amount)
  await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
}

function pressEnterInTheAmountToAdd(): void {
  amountToAddInput().dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }))
}

async function dialogAfterTheLaptopRefusedAddingMilk(): Promise<void> {
  recipeLaptop().answers(
    'PUT',
    /./,
    refusal('errors.admin.ingredients.amountInvalid', { status: 422, code: 'UnprocessableEntity' }),
  )
  await loadIngredientsAndMountDialog()
  await openTheAmountRowOf('ingredient-milch')
  await typeTheAmountToAdd('250')
  onScreen('[data-test="add-to-recipe"]').click()
  await waitUntil(() => expect(isOnScreen(MILK_REFUSAL)).toBe(true))
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the plus on an ingredient', () => {
  it('opens an empty amount field under that row with the unit of the ingredient inside it', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()

    await openTheAmountRowOf('ingredient-milch')

    expect(amountToAddInput().value).toBe('')
    expect(textOnScreen(`${MILK_ROW} ${AMOUNT_TO_ADD} [data-test="amount-input"] [data-test="amount-unit"]`).trim()).toBe('ml')
  })

  it.each([
    ['de', 'Hinzufügen'],
    ['en', 'Add'],
  ] as const)('offers the add button in %s beside the amount', async (locale, expected) => {
    recipeLaptop()
    await loadIngredientsAndMountDialog(locale)

    await openTheAmountRowOf('ingredient-milch')

    expect(onScreen(`${MILK_ROW} [data-test="add-to-recipe"]`).textContent?.trim()).toBe(expected)
  })

  it('keeps one row open at a time', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()
    await openTheAmountRowOf('ingredient-milch')

    await openTheAmountRowOf('ingredient-sahne')

    expect(ingredientIdsOfRowsHolding(AMOUNT_TO_ADD)).toEqual(['ingredient-sahne'])
  })

  it('keeps the amount already typed when it is pressed again on the open row', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()
    await openTheAmountRowOf('ingredient-milch')
    await typeTheAmountToAdd('250')

    await openTheAmountRowOf('ingredient-milch')

    expect(amountToAddInput().value).toBe('250')
    expect(addButtonIsDisabled()).toBe(false)
  })
})

describe('adding an ingredient with its amount', () => {
  it.each([
    ['no amount', ''],
    ['zero', '0'],
    ['unreadable text', 'viel'],
  ])('is not possible with %s', async (_description, amount) => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()
    await openTheAmountRowOf('ingredient-milch')

    typeInto(amountToAddInput(), amount)
    await flushPromises()

    expect(addButtonIsDisabled()).toBe(true)
  })

  it('sends one request with the typed amount for this article', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    await openTheAmountRowOf('ingredient-milch')
    await typeTheAmountToAdd('250,5')

    onScreen('[data-test="add-to-recipe"]').click()

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        { url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-milch', method: 'PUT', body: { amount: 250.5 } },
      ]),
    )
  })

  it('sends exactly one request when Enter is pressed in the amount field', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    await openTheAmountRowOf('ingredient-milch')
    await typeTheAmountToAdd('250')

    pressEnterInTheAmountToAdd()
    await flushPromises()

    expect(laptop.writes()).toEqual([
      { url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-milch', method: 'PUT', body: { amount: 250 } },
    ])
  })

  it('sends nothing when Enter is pressed without an amount', async () => {
    const laptop = recipeLaptop()
    await loadIngredientsAndMountDialog()
    await openTheAmountRowOf('ingredient-milch')

    pressEnterInTheAmountToAdd()
    await flushPromises()

    expect(laptop.writes()).toEqual([])
  })

  it('closes the row once the laptop has accepted the amount', async () => {
    recipeLaptop()
    await loadIngredientsAndMountDialog()
    await openTheAmountRowOf('ingredient-milch')
    await typeTheAmountToAdd('250')

    onScreen('[data-test="add-to-recipe"]').click()

    await waitUntil(() => expect(isOnScreen(AMOUNT_TO_ADD)).toBe(false))
  })
})

describe('an amount the laptop refuses to add', () => {
  it('is named under that row only, and the typed amount stays', async () => {
    await dialogAfterTheLaptopRefusedAddingMilk()

    expect(onScreen(MILK_REFUSAL).textContent?.trim()).toBe('Geben Sie eine Menge größer als null an.')
    expect(allOnScreen('[data-test="refusal"]')).toHaveLength(1)
    expect(amountToAddInput().value).toBe('250')
  })

  it('stops being named as soon as the admin types another amount', async () => {
    await dialogAfterTheLaptopRefusedAddingMilk()

    await typeIn(amountToAddInput(), '300')

    await waitUntil(() => expect(isOnScreen(MILK_REFUSAL)).toBe(false))
  })

  it('stops being named once the admin opens another row', async () => {
    await dialogAfterTheLaptopRefusedAddingMilk()

    await openTheAmountRowOf('ingredient-sahne')

    expect(isOnScreen(MILK_REFUSAL)).toBe(false)
  })
})
