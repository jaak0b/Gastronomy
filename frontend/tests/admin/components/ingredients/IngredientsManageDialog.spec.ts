import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { VSelect } from 'vuetify/components'
import IngredientsManageDialog from '../../../../src/admin/components/ingredients/IngredientsManageDialog.vue'
import { useAdminIngredientsStore } from '../../../../src/admin/stores/ingredients'
import { testPlugins } from '../../../support/plugins'
import { waitForDialog, onScreen, allOnScreen, typeInto, inputOf } from '../../../support/dom'
import { stubLaptop, answer, emptyAnswer, type StubbedLaptop } from '../../../support/laptop'

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

function ingredientsLaptop(): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(emptyAnswer(204))
    .answers('GET', /./, answer({ ingredients: [BUN, FLOUR, OLD_MILK] }))
}

async function mountDialog(locale: 'de' | 'en' = 'de'): Promise<VueWrapper> {
  await useAdminIngredientsStore().load()
  const dialog = mount(IngredientsManageDialog, {
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
  await vi.waitFor(() => expect(allOnScreen('[data-test="ingredient-edit-line"]').length).toBe(3))
  return dialog
}

function lineOf(ingredientId: string): string {
  return `[data-test="ingredient-edit-line"][data-test-id="${ingredientId}"]`
}

function nameInputs(): HTMLInputElement[] {
  return allOnScreen('[data-test="ingredient-edit-line"] [data-test="ingredient-name-field"] input') as HTMLInputElement[]
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
    ingredientsLaptop()

    await mountDialog()

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Zutaten')
    expect(nameInputs().map((input) => input.value)).toEqual(['Brötchen', 'Mehl', 'Milch'])
    expect(onScreen('[data-test="form-cancel"]').textContent?.trim()).toBe('Schließen')
  })

  it('offers the three units in English', async () => {
    ingredientsLaptop()
    const dialog = await mountDialog('en')

    const offered = dialog.getComponent<typeof VSelect>(`${lineOf('ingredient-broetchen')} [data-test="ingredient-unit-field"]`).props('items') as { title: string }[]

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Ingredients')
    expect(offered.map((choice) => choice.title)).toEqual(['pieces', 'g', 'ml'])
  })

  it('renames an ingredient as soon as the admin leaves the field', async () => {
    const laptop = ingredientsLaptop()
    await mountDialog()
    const flourName = inputOf(`${lineOf('ingredient-mehl')} [data-test="ingredient-name-field"]`)

    typeInto(flourName, 'Weizenmehl')
    flourName.dispatchEvent(new FocusEvent('blur'))

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        {
          url: '/api/admin/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { name: 'Weizenmehl', unit: 'gram' },
        },
      ]),
    )
  })

  it('saves a changed unit at once', async () => {
    const laptop = ingredientsLaptop()
    const dialog = await mountDialog()

    await dialog.getComponent(`${lineOf('ingredient-mehl')} [data-test="ingredient-unit-field"]`).setValue('millilitre')

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        {
          url: '/api/admin/ingredients/ingredient-mehl',
          method: 'PUT',
          body: { name: 'Mehl', unit: 'millilitre' },
        },
      ]),
    )
  })

  it('keeps a name the admin is typing when the ingredients load again', async () => {
    ingredientsLaptop()
    await mountDialog()
    const flourName = inputOf(`${lineOf('ingredient-mehl')} [data-test="ingredient-name-field"]`)

    typeInto(flourName, 'Weizenmehl')
    await useAdminIngredientsStore().load()

    await vi.waitFor(() => expect(allOnScreen('[data-test="ingredient-edit-line"]').length).toBe(3))
    expect(flourName.value).toBe('Weizenmehl')
  })

  it('deactivates an ingredient after the admin confirms', async () => {
    const laptop = ingredientsLaptop()
    await mountDialog()

    onScreen(`${lineOf('ingredient-broetchen')} [data-test="deactivate-ingredient"]`).click()
    await waitForDialog()
    expect(onScreen('[data-test="confirm-title"]').textContent).toBe('Zutat deaktivieren?')
    expect(onScreen('[data-test="confirm-body"]').textContent).toBe(
      'Eine deaktivierte Zutat begrenzt den Verkauf nicht mehr.',
    )
    onScreen('[data-test="confirm-dialog"] [data-test="confirm"]').click()

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/ingredients/ingredient-broetchen/deactivate',
      ]),
    )
  })

  it('activates a deactivated ingredient', async () => {
    const laptop = ingredientsLaptop()
    await mountDialog()

    onScreen('[data-test="activate-ingredient"]').click()

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/ingredients/ingredient-milch/activate',
      ]),
    )
  })
})
