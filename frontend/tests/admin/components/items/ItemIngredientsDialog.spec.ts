import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { VCombobox, VSelect } from 'vuetify/components'
import ItemIngredientsDialog from '../../../../src/admin/components/items/ItemIngredientsDialog.vue'
import { useAdminIngredientsStore } from '../../../../src/admin/stores/ingredients'
import type { AdminItemView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'

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

interface Call {
  url: string
  method: string
  body: unknown
}

interface Refusal {
  status: number
  body: unknown
}

function stubLaptop(refuses: (call: Call) => Refusal | null = () => null): Call[] {
  const calls: Call[] = []
  let eggWasCreated = false
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      const call = {
        url,
        method,
        body: init?.body === undefined ? null : JSON.parse(String(init.body)),
      }
      calls.push(call)
      const refusal = refuses(call)
      if (refusal !== null) {
        return new Response(JSON.stringify(refusal.body), { status: refusal.status })
      }
      if (url === '/api/admin/ingredients' && method === 'POST') {
        eggWasCreated = true
        return new Response(JSON.stringify(CREATED_EGG), { status: 201 })
      }
      if (url === '/api/admin/ingredients') {
        const ingredients = eggWasCreated ? [BUN, CREATED_EGG, FLOUR, MILK] : [BUN, FLOUR, MILK]
        return new Response(JSON.stringify({ ingredients }), { status: 200 })
      }
      if (method === 'DELETE') {
        return new Response(null, { status: 204 })
      }
      if (method !== 'GET') {
        return new Response(JSON.stringify({}), { status: 200 })
      }
      return new Response(JSON.stringify({ items: [PANCAKE] }), { status: 200 })
    }),
  )
  return calls
}

function refusalOf(messageKey: string): Refusal {
  return {
    status: 422,
    body: { code: 'UnprocessableEntity', messageKey, parameters: {}, details: null },
  }
}

async function openDialog(locale: 'de' | 'en' = 'de'): Promise<VueWrapper> {
  await useAdminIngredientsStore().load()
  const dialog = mount(ItemIngredientsDialog, {
    props: { item: PANCAKE },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
  await vi.waitFor(() => expect(document.querySelector('.recipe-line')).not.toBeNull())
  return dialog
}

function inDialog(selector: string): HTMLElement {
  return document.querySelector(selector) as HTMLElement
}

function allInDialog(selector: string): HTMLElement[] {
  return [...document.querySelectorAll<HTMLElement>(selector)]
}

function typeInto(field: HTMLElement, value: string): void {
  const input = field as HTMLInputElement
  input.value = value
  input.dispatchEvent(new Event('input'))
}

function leave(field: HTMLElement): void {
  field.dispatchEvent(new FocusEvent('blur'))
}

function addedAmountInput(): HTMLInputElement {
  return inDialog('.add-recipe-line .amount-input input') as HTMLInputElement
}

function addButtonIsDisabled(): boolean {
  return inDialog('.add-to-recipe').hasAttribute('disabled')
}

async function chooseUnit(dialog: VueWrapper, entryUnit: string): Promise<void> {
  await dialog.findAllComponents(VSelect).at(-1)?.setValue(entryUnit)
}

function written(calls: Call[]): Call[] {
  return calls.filter((call) => call.method !== 'GET')
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
    stubLaptop()

    await openDialog()

    expect(inDialog('.form-dialog-title').textContent).toBe('Zutaten für Pfannkuchen')
    expect(allInDialog('.recipe-ingredient-name').map((name) => name.textContent)).toEqual([
      'Mehl',
      'Brötchen',
    ])
    const amounts = allInDialog('.recipe-line .amount-input input') as HTMLInputElement[]
    expect(amounts.map((input) => input.value)).toEqual(['1500', '2'])
    expect(allInDialog('.recipe-line .amount-unit').map((unit) => unit.textContent)).toEqual([
      'g',
      'Stück',
    ])
  })

  it('show the title and the units in English', async () => {
    stubLaptop()

    await openDialog('en')

    expect(inDialog('.form-dialog-title').textContent).toBe('Ingredients for Pfannkuchen')
    expect(allInDialog('.recipe-line .amount-unit').map((unit) => unit.textContent)).toEqual([
      'g',
      'pieces',
    ])
  })

  it('save a changed decimal amount as soon as the admin leaves the field', async () => {
    const calls = stubLaptop()
    await openDialog()
    const flourAmount = allInDialog('.recipe-line .amount-input input')[0]

    typeInto(flourAmount, '2250,5')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
        {
          url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { amount: 2250.5 },
        },
      ]),
    )
  })

  it('remove an ingredient from the recipe', async () => {
    const calls = stubLaptop()
    await openDialog()

    allInDialog('.remove-recipe-line')[1].click()

    await vi.waitFor(() =>
      expect(written(calls).map((call) => `${call.method} ${call.url}`)).toEqual([
        'DELETE /api/admin/items/item-pfannkuchen/ingredients/ingredient-broetchen',
      ]),
    )
  })

  it('refuse an amount that is not a number without sending it and keep what was typed', async () => {
    const calls = stubLaptop()
    await openDialog()
    const flourAmount = allInDialog('.recipe-line .amount-input input')[0] as HTMLInputElement

    typeInto(flourAmount, 'viel')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(inDialog('.form-dialog .refusal').textContent?.trim()).toBe(
        'Geben Sie eine Menge größer als null an.',
      ),
    )
    expect(written(calls)).toEqual([])
    expect(flourAmount.value).toBe('viel')
  })

  it('show the reason in English when the laptop refuses an amount', async () => {
    stubLaptop((call) =>
      call.method === 'PUT' ? refusalOf('errors.admin.ingredients.amountInvalid') : null,
    )
    await openDialog('en')
    const flourAmount = allInDialog('.recipe-line .amount-input input')[0]

    typeInto(flourAmount, '0')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(inDialog('.form-dialog .refusal').textContent?.trim()).toBe(
        'Enter an amount above zero.',
      ),
    )
  })
})

describe('adding an ingredient to the recipe', () => {
  it('offers only the active ingredients that are not yet in the recipe', async () => {
    stubLaptop()
    const dialog = await openDialog()

    const offered = dialog.findComponent(VCombobox).props('items') as { name: string }[]

    expect(offered.map((ingredient) => ingredient.name)).toEqual(['Milch'])
  })

  it('adds an existing ingredient with one request for the amount', async () => {
    const calls = stubLaptop()
    const dialog = await openDialog()

    await dialog.findComponent(VCombobox).setValue(MILK)
    typeInto(addedAmountInput(), '250')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    inDialog('.add-to-recipe').click()

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
        {
          url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-milch',
          method: 'PUT',
          body: { amount: 250 },
        },
      ]),
    )
  })

  it('offers only the units of the chosen ingredient', async () => {
    stubLaptop()
    const dialog = await openDialog()

    await dialog.findComponent(VCombobox).setValue(MILK)

    await vi.waitFor(() =>
      expect(
        (dialog.findAllComponents(VSelect).at(-1)?.props('items') as { title: string }[]).map(
          (choice) => choice.title,
        ),
      ).toEqual(['ml', 'l']),
    )
  })

  it('sends an amount typed in litres in millilitres', async () => {
    const calls = stubLaptop()
    const dialog = await openDialog()

    await dialog.findComponent(VCombobox).setValue(MILK)
    await chooseUnit(dialog, 'litre')
    typeInto(addedAmountInput(), '0,25')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    inDialog('.add-to-recipe').click()

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
        {
          url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-milch',
          method: 'PUT',
          body: { amount: 250 },
        },
      ]),
    )
  })

  it('creates a typed new name in grams when the amount was typed in kilograms, then adds it', async () => {
    const calls = stubLaptop()
    const dialog = await openDialog()

    await dialog.findComponent(VCombobox).setValue('Zucker')
    await chooseUnit(dialog, 'kilogram')
    typeInto(addedAmountInput(), '1,5')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    inDialog('.add-to-recipe').click()

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
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
    const calls = stubLaptop()
    const dialog = await openDialog()

    await dialog.findComponent(VCombobox).setValue('Milch')
    typeInto(addedAmountInput(), '100')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    inDialog('.add-to-recipe').click()

    await vi.waitFor(() =>
      expect(written(calls).map((call) => `${call.method} ${call.url}`)).toEqual([
        'PUT /api/admin/items/item-pfannkuchen/ingredients/ingredient-milch',
      ]),
    )
  })

  it('clears the add row after a successful add', async () => {
    stubLaptop()
    const dialog = await openDialog()

    await dialog.findComponent(VCombobox).setValue(MILK)
    typeInto(addedAmountInput(), '250')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    inDialog('.add-to-recipe').click()

    await vi.waitFor(() => expect(dialog.findComponent(VCombobox).props('modelValue')).toBeNull())
    expect(addedAmountInput().value).toBe('')
  })

  it.each([
    ['no name', null, '250'],
    ['no amount', MILK, ''],
    ['zero', MILK, '0'],
    ['unreadable text', MILK, 'viel'],
  ])('keeps the add button disabled with %s', async (_description, chosen, amount) => {
    stubLaptop()
    const dialog = await openDialog()

    await dialog.findComponent(VCombobox).setValue(chosen)
    typeInto(addedAmountInput(), amount)
    await dialog.vm.$nextTick()

    expect(addButtonIsDisabled()).toBe(true)
  })

  it('shows the reason in German when the laptop refuses the new name, and keeps everything typed', async () => {
    const calls = stubLaptop((call) =>
      call.method === 'POST' ? refusalOf('errors.admin.ingredients.nameTaken') : null,
    )
    const dialog = await openDialog()

    await dialog.findComponent(VCombobox).setValue('Mehl')
    typeInto(addedAmountInput(), '100')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    inDialog('.add-to-recipe').click()

    await vi.waitFor(() =>
      expect(inDialog('.form-dialog .refusal').textContent?.trim()).toBe(
        'Eine Zutat mit diesem Namen gibt es schon.',
      ),
    )
    expect(dialog.findComponent(VCombobox).props('modelValue')).toBe('Mehl')
    expect(addedAmountInput().value).toBe('100')
    expect(written(calls).map((call) => call.method)).toEqual(['POST'])
  })

  it('keeps the created ingredient chosen and the amount when the laptop refuses the amount, and sends only the amount again', async () => {
    const calls = stubLaptop((call) =>
      call.method === 'PUT' ? refusalOf('errors.admin.ingredients.amountInvalid') : null,
    )
    const dialog = await openDialog()

    await dialog.findComponent(VCombobox).setValue('Ei')
    typeInto(addedAmountInput(), '3')
    await vi.waitFor(() => expect(addButtonIsDisabled()).toBe(false))
    inDialog('.add-to-recipe').click()

    await vi.waitFor(() =>
      expect(inDialog('.form-dialog .refusal').textContent?.trim()).toBe(
        'Geben Sie eine Menge größer als null an.',
      ),
    )
    expect(dialog.findComponent(VCombobox).props('modelValue')).toEqual(CREATED_EGG)
    expect(addedAmountInput().value).toBe('3')
    inDialog('.add-to-recipe').click()

    await vi.waitFor(() =>
      expect(written(calls).map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/ingredients',
        'PUT /api/admin/items/item-pfannkuchen/ingredients/ingredient-ei',
        'PUT /api/admin/items/item-pfannkuchen/ingredients/ingredient-ei',
      ]),
    )
  })
})
