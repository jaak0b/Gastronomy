import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { VSelect } from 'vuetify/components'
import IngredientsManageDialog from '../../../../src/admin/components/ingredients/IngredientsManageDialog.vue'
import { useAdminIngredientsStore } from '../../../../src/admin/stores/ingredients'
import { testPlugins, waitForDialog } from '../../../support/plugins'

const FLOUR = { ingredientId: 'ingredient-mehl', name: 'Mehl', unit: 'gram', isActive: true }
const BUN = { ingredientId: 'ingredient-broetchen', name: 'Brötchen', unit: 'piece', isActive: true }
const OLD_MILK = {
  ingredientId: 'ingredient-milch',
  name: 'Milch',
  unit: 'millilitre',
  isActive: false,
}

interface Call {
  url: string
  method: string
  body: unknown
}

function stubLaptop(): Call[] {
  const calls: Call[] = []
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string, init?: RequestInit) => {
      const method = init?.method ?? 'GET'
      calls.push({
        url,
        method,
        body: init?.body === undefined ? null : JSON.parse(String(init.body)),
      })
      if (method !== 'GET') {
        return new Response(null, { status: 204 })
      }
      return new Response(JSON.stringify({ ingredients: [BUN, FLOUR, OLD_MILK] }), {
        status: 200,
      })
    }),
  )
  return calls
}

async function openDialog(locale: 'de' | 'en' = 'de'): Promise<VueWrapper> {
  await useAdminIngredientsStore().load()
  const dialog = mount(IngredientsManageDialog, {
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
  await vi.waitFor(() => expect(allInDialog('.ingredient-edit-line').length).toBe(3))
  return dialog
}

function inDialog(selector: string): HTMLElement {
  return document.querySelector(selector) as HTMLElement
}

function allInDialog(selector: string): HTMLElement[] {
  return [...document.querySelectorAll<HTMLElement>(selector)]
}

function nameInputs(): HTMLInputElement[] {
  return allInDialog('.ingredient-edit-line .ingredient-name-field input') as HTMLInputElement[]
}

function typeInto(input: HTMLInputElement, value: string): void {
  input.value = value
  input.dispatchEvent(new Event('input'))
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

describe('the ingredient management dialog', () => {
  it('lists every ingredient under its German title', async () => {
    stubLaptop()

    await openDialog()

    expect(inDialog('.form-dialog-title').textContent).toBe('Zutaten')
    expect(nameInputs().map((input) => input.value)).toEqual(['Brötchen', 'Mehl', 'Milch'])
    expect(inDialog('.form-cancel').textContent?.trim()).toBe('Schließen')
  })

  it('offers the three units in English', async () => {
    stubLaptop()
    const dialog = await openDialog('en')

    const offered = dialog.findAllComponents(VSelect)[0].props('items') as { title: string }[]

    expect(inDialog('.form-dialog-title').textContent).toBe('Ingredients')
    expect(offered.map((choice) => choice.title)).toEqual(['pieces', 'g', 'ml'])
  })

  it('renames an ingredient as soon as the admin leaves the field', async () => {
    const calls = stubLaptop()
    await openDialog()
    const flourName = nameInputs()[1]

    typeInto(flourName, 'Weizenmehl')
    flourName.dispatchEvent(new FocusEvent('blur'))

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

    await dialog.findAllComponents(VSelect)[1].setValue('millilitre')

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
    const flourName = nameInputs()[1]

    typeInto(flourName, 'Weizenmehl')
    await useAdminIngredientsStore().load()

    await vi.waitFor(() => expect(allInDialog('.ingredient-edit-line').length).toBe(3))
    expect(flourName.value).toBe('Weizenmehl')
  })

  it('deactivates an ingredient after the admin confirms', async () => {
    const calls = stubLaptop()
    await openDialog()

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

  it('activates a deactivated ingredient', async () => {
    const calls = stubLaptop()
    await openDialog()

    inDialog('.activate-ingredient').click()

    await vi.waitFor(() =>
      expect(written(calls).map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/ingredients/ingredient-milch/activate',
      ]),
    )
  })
})
