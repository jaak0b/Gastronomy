import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { VSelect } from 'vuetify/components'
import ItemIngredientsDialog from '../../../../src/admin/components/items/ItemIngredientsDialog.vue'
import { useAdminIngredientsStore } from '../../../../src/admin/stores/ingredients'
import type { AdminItemView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins, waitForDialog } from '../../../support/plugins'

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
  it('show each ingredient with its amount in the larger unit from 1000 on', async () => {
    stubLaptop()

    await openDialog()

    expect(inDialog('.form-dialog-title').textContent).toBe('Zutaten für Pfannkuchen')
    expect(allInDialog('.recipe-ingredient-name').map((name) => name.textContent)).toEqual([
      'Mehl',
      'Brötchen',
    ])
    const amounts = allInDialog('.recipe-line .amount-input input') as HTMLInputElement[]
    expect(amounts.map((input) => input.value)).toEqual(['1,5', '2'])
    expect(inDialog('.recipe-line .entry-unit-kilogram').classList).toContain('v-btn--active')
    expect(allInDialog('.recipe-line .pieces-word').map((word) => word.textContent)).toEqual([
      'Stück',
    ])
  })

  it('write the amount with a decimal point and the English words in English', async () => {
    stubLaptop()

    await openDialog('en')

    expect(inDialog('.form-dialog-title').textContent).toBe('Ingredients for Pfannkuchen')
    const amounts = allInDialog('.recipe-line .amount-input input') as HTMLInputElement[]
    expect(amounts.map((input) => input.value)).toEqual(['1.5', '2'])
    expect(inDialog('.recipe-line .pieces-word').textContent).toBe('pieces')
  })

  it('save a changed amount in grams as soon as the admin leaves the field', async () => {
    const calls = stubLaptop()
    await openDialog()
    const flourAmount = allInDialog('.recipe-line .amount-input input')[0]

    typeInto(flourAmount, '2,25')
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
        {
          url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { amount: 2250 },
        },
      ]),
    )
  })

  it('show the saved amount in grams when the admin switches the unit, and send nothing', async () => {
    const calls = stubLaptop()
    await openDialog()
    const flourAmount = allInDialog('.recipe-line .amount-input input')[0] as HTMLInputElement

    inDialog('.recipe-line .entry-unit-gram').click()

    await vi.waitFor(() => expect(flourAmount.value).toBe('1500'))
    expect(written(calls)).toEqual([])
  })

  it('convert a typed amount on a unit switch and save it once when the admin leaves the field', async () => {
    const calls = stubLaptop()
    await openDialog()
    const flourAmount = allInDialog('.recipe-line .amount-input input')[0] as HTMLInputElement

    typeInto(flourAmount, '2')
    inDialog('.recipe-line .entry-unit-gram').click()
    await vi.waitFor(() => expect(flourAmount.value).toBe('2000'))
    expect(written(calls)).toEqual([])
    leave(flourAmount)

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
        {
          url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { amount: 2000 },
        },
      ]),
    )
  })

  it('keep unreadable text exactly as typed when the admin switches the unit', async () => {
    const calls = stubLaptop()
    await openDialog()
    const flourAmount = allInDialog('.recipe-line .amount-input input')[0] as HTMLInputElement

    typeInto(flourAmount, 'viel')
    inDialog('.recipe-line .entry-unit-gram').click()

    await vi.waitFor(() =>
      expect(inDialog('.recipe-line .entry-unit-gram').classList).toContain('v-btn--active'),
    )
    expect(flourAmount.value).toBe('viel')
    expect(written(calls)).toEqual([])
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
})

describe('adding an ingredient to the recipe', () => {
  it('adds an existing ingredient with the amount converted from litres', async () => {
    const calls = stubLaptop()
    const dialog = await openDialog()

    await dialog.findComponent(VSelect).setValue('ingredient-milch')
    await vi.waitFor(() => expect(inDialog('.add-recipe-line .entry-unit-litre')).not.toBeNull())
    inDialog('.add-recipe-line .entry-unit-litre').click()
    typeInto(inDialog('.add-recipe-line .amount-input input'), '0,25')
    await vi.waitFor(() => expect(inDialog('.form-save').hasAttribute('disabled')).toBe(false))
    inDialog('.form-save').click()

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

  it('offers only the active ingredients that are not yet in the recipe', async () => {
    stubLaptop()
    const dialog = await openDialog()

    const offered = dialog.findComponent(VSelect).props('items') as { name: string }[]

    expect(offered.map((ingredient) => ingredient.name)).toEqual(['Milch'])
  })

  it('creates a new ingredient and adds it with the amount', async () => {
    const calls = stubLaptop()
    const dialog = await openDialog()

    inDialog('.start-new-ingredient').click()
    await vi.waitFor(() => expect(inDialog('.new-ingredient-name input')).not.toBeNull())
    typeInto(inDialog('.new-ingredient-name input'), 'Ei')
    await dialog.findAllComponents(VSelect).at(-1)?.setValue('piece')
    await vi.waitFor(() => expect(inDialog('.add-recipe-line .pieces-word')).not.toBeNull())
    typeInto(inDialog('.add-recipe-line .amount-input input'), '3')
    await vi.waitFor(() => expect(inDialog('.form-save').hasAttribute('disabled')).toBe(false))
    inDialog('.form-save').click()

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
        { url: '/api/admin/ingredients', method: 'POST', body: { name: 'Ei', unit: 'piece' } },
        {
          url: '/api/admin/items/item-pfannkuchen/ingredients/ingredient-ei',
          method: 'PUT',
          body: { amount: 3 },
        },
      ]),
    )
  })

  it('shows the reason in German when the laptop refuses the new name, and keeps the name', async () => {
    stubLaptop((call) =>
      call.method === 'POST' ? refusalOf('errors.admin.ingredients.nameTaken') : null,
    )
    await openDialog()

    inDialog('.start-new-ingredient').click()
    await vi.waitFor(() => expect(inDialog('.new-ingredient-name input')).not.toBeNull())
    typeInto(inDialog('.new-ingredient-name input'), 'Mehl')
    typeInto(inDialog('.add-recipe-line .amount-input input'), '100')
    await vi.waitFor(() => expect(inDialog('.form-save').hasAttribute('disabled')).toBe(false))
    inDialog('.form-save').click()

    await vi.waitFor(() =>
      expect(inDialog('.form-dialog .refusal').textContent?.trim()).toBe(
        'Eine Zutat mit diesem Namen gibt es schon.',
      ),
    )
    expect((inDialog('.new-ingredient-name input') as HTMLInputElement).value).toBe('Mehl')
  })

  it('keeps the new ingredient and the amount when the laptop refuses the amount, and sends only the amount again', async () => {
    const calls = stubLaptop((call) =>
      call.method === 'PUT' ? refusalOf('errors.admin.ingredients.amountInvalid') : null,
    )
    const dialog = await openDialog()

    inDialog('.start-new-ingredient').click()
    await vi.waitFor(() => expect(inDialog('.new-ingredient-name input')).not.toBeNull())
    typeInto(inDialog('.new-ingredient-name input'), 'Ei')
    await dialog.findAllComponents(VSelect).at(-1)?.setValue('piece')
    await vi.waitFor(() => expect(inDialog('.add-recipe-line .pieces-word')).not.toBeNull())
    typeInto(inDialog('.add-recipe-line .amount-input input'), '3')
    await vi.waitFor(() => expect(inDialog('.form-save').hasAttribute('disabled')).toBe(false))
    inDialog('.form-save').click()

    await vi.waitFor(() =>
      expect(inDialog('.form-dialog .refusal').textContent?.trim()).toBe(
        'Geben Sie eine Menge größer als null an.',
      ),
    )
    expect(dialog.findComponent(VSelect).props('modelValue')).toBe('ingredient-ei')
    expect((inDialog('.add-recipe-line .amount-input input') as HTMLInputElement).value).toBe('3')
    inDialog('.form-save').click()

    await vi.waitFor(() =>
      expect(written(calls).map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/ingredients',
        'PUT /api/admin/items/item-pfannkuchen/ingredients/ingredient-ei',
        'PUT /api/admin/items/item-pfannkuchen/ingredients/ingredient-ei',
      ]),
    )
  })

  it('shows the reason in English when the laptop refuses the amount', async () => {
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

describe('the list of all ingredients', () => {
  async function openTheList(): Promise<void> {
    inDialog('.all-ingredients-title').click()
    await vi.waitFor(() => expect(allInDialog('.ingredient-edit-line').length).toBe(3))
  }

  it('renames an ingredient as soon as the admin leaves the field', async () => {
    const calls = stubLaptop()
    await openDialog()
    await openTheList()
    const flourName = allInDialog('.ingredient-edit-line .ingredient-name-field input')[1]

    typeInto(flourName, 'Weizenmehl')
    leave(flourName)

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
        {
          url: '/api/admin/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { name: 'Weizenmehl', unit: 'gram' },
        },
      ]),
    )
  })

  it('saves a changed unit at once', async () => {
    const calls = stubLaptop()
    const dialog = await openDialog()
    await openTheList()

    await dialog.findAllComponents(VSelect).at(-2)?.setValue('millilitre')

    await vi.waitFor(() =>
      expect(written(calls)).toEqual([
        {
          url: '/api/admin/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { name: 'Mehl', unit: 'millilitre' },
        },
      ]),
    )
  })

  it('keeps a name the admin is typing when the ingredients load again', async () => {
    stubLaptop()
    await openDialog()
    await openTheList()
    const flourName = allInDialog(
      '.ingredient-edit-line .ingredient-name-field input',
    )[1] as HTMLInputElement

    typeInto(flourName, 'Weizenmehl')
    await useAdminIngredientsStore().load()

    await vi.waitFor(() => expect(allInDialog('.ingredient-edit-line').length).toBe(3))
    expect(flourName.value).toBe('Weizenmehl')
  })

  it('deactivates an ingredient after the admin confirms', async () => {
    const calls = stubLaptop()
    await openDialog()
    await openTheList()

    allInDialog('.deactivate-ingredient')[0].click()
    await waitForDialog()
    expect(inDialog('.confirm-title').textContent).toBe('Zutat deaktivieren?')
    expect(inDialog('.confirm-body').textContent).toBe(
      'Eine deaktivierte Zutat begrenzt den Verkauf nicht mehr.',
    )
    inDialog('.confirm-dialog .confirm').click()

    await vi.waitFor(() =>
      expect(written(calls).map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/ingredients/ingredient-broetchen/deactivate',
      ]),
    )
  })
})
