import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { VCombobox, VSelect } from 'vuetify/components'
import ItemIngredientsDialog from '../../../../src/admin/components/items/ItemIngredientsDialog.vue'
import { useAdminIngredientsStore } from '../../../../src/admin/stores/ingredients'
import type { AdminItemView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { stubLaptop, answer, emptyAnswer, refusal, type LaptopCall, type LaptopReply, type StubbedLaptop } from '../../../support/laptop'
import { onScreen, allOnScreen, typeInto, leave, inputOf } from '../../../support/dom'
import { nextTick } from 'vue'

const FLOUR = { ingredientId: 'ingredient-mehl', name: 'Mehl', unit: 'gram', isActive: true }
const MILK = { ingredientId: 'ingredient-milch', name: 'Milch', unit: 'millilitre', isActive: true }
const BUN = { ingredientId: 'ingredient-broetchen', name: 'Brötchen', unit: 'piece', isActive: true }
const CREATED_EGG = { ingredientId: 'ingredient-ei', name: 'Ei', unit: 'piece', isActive: true }

const PANCAKE: AdminItemView = {
  itemId: 'item-pfannkuchen',
  name: 'Pfannkuchen',
  categoryId: 'category-speisen',
  sortOrder: 1,
  isActive: true,
  productionMinutes: null,
  isQueueIndependent: false,
  atTheFestival: null,
  ingredients: [
    { ingredientId: 'ingredient-mehl', amount: 1500 },
    { ingredientId: 'ingredient-broetchen', amount: 2 },
  ],
}

function recipeLaptop(refuses: (call: LaptopCall) => LaptopReply | null = () => null): StubbedLaptop {
  let eggWasCreated = false
  return stubLaptop()
    .answersEverythingElse(answer({}))
    .answers('GET', /./, answer({ items: [PANCAKE] }))
    .answers('DELETE', /./, emptyAnswer(204))
    .answers('GET', (call) => call.url === '/api/admin/ingredients', (call) =>
      answer({
        ingredients: eggWasCreated ? [BUN, CREATED_EGG, FLOUR, MILK] : [BUN, FLOUR, MILK],
      })(call),
    )
    .answers('POST', (call) => call.url === '/api/admin/ingredients', (call) => {
      eggWasCreated = true
      return answer(CREATED_EGG, 201)(call)
    })
    .answers('ANY', (call) => refuses(call) !== null, (call) => (refuses(call) as LaptopReply)(call))
}

async function mountDialog(locale: 'de' | 'en' = 'de'): Promise<VueWrapper> {
  await useAdminIngredientsStore().load()
  const dialog = mount(ItemIngredientsDialog, {
    props: { item: PANCAKE },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
  await vi.waitFor(() => expect(document.querySelector('[data-test="recipe-line"]')).not.toBeNull())
  return dialog
}

function addedAmountInput(): HTMLInputElement {
  return onScreen('[data-test="add-recipe-line"] [data-test="amount-input"] input') as HTMLInputElement
}

function addButtonIsDisabled(): boolean {
  return onScreen('[data-test="add-to-recipe"]').hasAttribute('disabled')
}

async function chooseUnit(dialog: VueWrapper, entryUnit: string): Promise<void> {
  await dialog.getComponent('[data-test="add-recipe-line"] [data-test="amount-unit-select"]').setValue(entryUnit)
}

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

    await mountDialog()

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

    await mountDialog('en')

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Ingredients for Pfannkuchen')
    expect(allOnScreen('[data-test="recipe-line"] [data-test="amount-unit"]').map((unit) => unit.textContent)).toEqual([
      'g',
      'pieces',
    ])
  })

  it('save a changed decimal amount as soon as the admin leaves the field', async () => {
    const laptop = recipeLaptop()
    await mountDialog()
    const flourAmount = inputOf('[data-test="recipe-line"][data-test-id="ingredient-mehl"] [data-test="amount-input"]')

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

  it('remove an ingredient from the recipe', async () => {
    const laptop = recipeLaptop()
    await mountDialog()

    onScreen('[data-test="recipe-line"][data-test-id="ingredient-broetchen"] [data-test="remove-recipe-line"]').click()

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'DELETE /api/admin/items/item-pfannkuchen/ingredients/ingredient-broetchen',
      ]),
    )
  })

  it('refuse an amount that is not a number without sending it and keep what was typed', async () => {
    const laptop = recipeLaptop()
    await mountDialog()
    const flourAmount = inputOf('[data-test="recipe-line"][data-test-id="ingredient-mehl"] [data-test="amount-input"]') as HTMLInputElement

    typeInto(flourAmount, 'viel')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Geben Sie eine Menge größer als null an.',
      ),
    )
    expect(laptop.writes()).toEqual([])
    expect(flourAmount.value).toBe('viel')
  })

  it('show the reason in English when the laptop refuses an amount', async () => {
    recipeLaptop((call) =>
      call.method === 'PUT' ? refusal('errors.admin.ingredients.amountInvalid', { status: 422, code: 'UnprocessableEntity' }) : null,
    )
    await mountDialog('en')
    const flourAmount = inputOf('[data-test="recipe-line"][data-test-id="ingredient-mehl"] [data-test="amount-input"]')

    typeInto(flourAmount, '0')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Enter an amount above zero.',
      ),
    )
  })
})

describe('adding an ingredient to the recipe', () => {
  it('offers only the active ingredients that are not yet in the recipe', async () => {
    recipeLaptop()
    const dialog = await mountDialog()

    const offered = dialog.findComponent(VCombobox).props('items') as { name: string }[]

    expect(offered.map((ingredient) => ingredient.name)).toEqual(['Milch'])
  })

  it('adds an existing ingredient with one request for the amount', async () => {
    const laptop = recipeLaptop()
    const dialog = await mountDialog()

    await dialog.findComponent(VCombobox).setValue(MILK)
    typeInto(addedAmountInput(), '250')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    onScreen('[data-test="add-to-recipe"]').click()

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

  it('offers only the units of the chosen ingredient', async () => {
    recipeLaptop()
    const dialog = await mountDialog()

    await dialog.findComponent(VCombobox).setValue(MILK)

    await vi.waitFor(() =>
      expect(
        (dialog.getComponent<typeof VSelect>('[data-test="add-recipe-line"] [data-test="amount-unit-select"]').props('items') as { title: string }[]).map(
          (choice) => choice.title,
        ),
      ).toEqual(['ml', 'l']),
    )
  })

  it('sends an amount typed in litres in millilitres', async () => {
    const laptop = recipeLaptop()
    const dialog = await mountDialog()

    await dialog.findComponent(VCombobox).setValue(MILK)
    await chooseUnit(dialog, 'litre')
    typeInto(addedAmountInput(), '0,25')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    onScreen('[data-test="add-to-recipe"]').click()

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

  it('creates a typed new name in grams when the amount was typed in kilograms, then adds it', async () => {
    const laptop = recipeLaptop()
    const dialog = await mountDialog()

    await dialog.findComponent(VCombobox).setValue('Zucker')
    await chooseUnit(dialog, 'kilogram')
    typeInto(addedAmountInput(), '1,5')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    onScreen('[data-test="add-to-recipe"]').click()

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        { url: '/api/admin/ingredients', method: 'POST', body: { name: 'Zucker', unit: 'gram' } },
        {
          url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-ei',
          method: 'PUT',
          body: { amount: 1500 },
        },
      ]),
    )
  })

  it('treats a typed name of an offered ingredient as that ingredient', async () => {
    const laptop = recipeLaptop()
    const dialog = await mountDialog()

    await dialog.findComponent(VCombobox).setValue('Milch')
    typeInto(addedAmountInput(), '100')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    onScreen('[data-test="add-to-recipe"]').click()

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'PUT /api/admin/items/item-pfannkuchen/ingredients/ingredient-milch',
      ]),
    )
  })

  it('clears the add row after a successful add', async () => {
    recipeLaptop()
    const dialog = await mountDialog()

    await dialog.findComponent(VCombobox).setValue(MILK)
    typeInto(addedAmountInput(), '250')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    onScreen('[data-test="add-to-recipe"]').click()

    await vi.waitFor(() => expect(dialog.findComponent(VCombobox).props('modelValue')).toBeNull())
    expect(addedAmountInput().value).toBe('')
  })

  it.each([
    ['no name', null, '250'],
    ['no amount', MILK, ''],
    ['zero', MILK, '0'],
    ['unreadable text', MILK, 'viel'],
  ])('keeps the add button disabled with %s', async (_description, chosen, amount) => {
    recipeLaptop()
    const dialog = await mountDialog()

    await dialog.findComponent(VCombobox).setValue(chosen)
    typeInto(addedAmountInput(), amount)
    await nextTick()

    expect(addButtonIsDisabled()).toBe(true)
  })

  it('shows the reason in German when the laptop refuses the new name, and keeps everything typed', async () => {
    const laptop = recipeLaptop((call) =>
      call.method === 'POST' ? refusal('errors.admin.ingredients.nameTaken', { status: 422, code: 'UnprocessableEntity' }) : null,
    )
    const dialog = await mountDialog()

    await dialog.findComponent(VCombobox).setValue('Mehl')
    typeInto(addedAmountInput(), '100')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    onScreen('[data-test="add-to-recipe"]').click()

    await vi.waitFor(() =>
      expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Eine Zutat mit diesem Namen gibt es schon.',
      ),
    )
    expect(dialog.findComponent(VCombobox).props('modelValue')).toBe('Mehl')
    expect(addedAmountInput().value).toBe('100')
    expect(laptop.writes().map((call) => call.method)).toEqual(['POST'])
  })

  it('keeps the created ingredient chosen and the amount when the laptop refuses the amount, and sends only the amount again', async () => {
    const laptop = recipeLaptop((call) =>
      call.method === 'PUT' ? refusal('errors.admin.ingredients.amountInvalid', { status: 422, code: 'UnprocessableEntity' }) : null,
    )
    const dialog = await mountDialog()

    await dialog.findComponent(VCombobox).setValue('Ei')
    typeInto(addedAmountInput(), '3')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    onScreen('[data-test="add-to-recipe"]').click()

    await vi.waitFor(() =>
      expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Geben Sie eine Menge größer als null an.',
      ),
    )
    expect(dialog.findComponent(VCombobox).props('modelValue')).toEqual(CREATED_EGG)
    expect(addedAmountInput().value).toBe('3')
    onScreen('[data-test="add-to-recipe"]').click()

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/ingredients',
        'PUT /api/admin/items/item-pfannkuchen/ingredients/ingredient-ei',
        'PUT /api/admin/items/item-pfannkuchen/ingredients/ingredient-ei',
      ]),
    )
  })
})
