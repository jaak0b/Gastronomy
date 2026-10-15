import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import IngredientDialog from '../../../../src/admin/components/ingredients/IngredientDialog.vue'
import { inputOf, onScreen } from '../../../support/dom'
import { saveTheIngredientDialog } from '../../../support/formDialogs'
import { answer, refusal } from '../../../support/laptop'
import { MUSTARD, ingredientsListLaptop, mountList } from './ingredientsListFixture'

const MUSTARD_ROW = `[data-test="ingredient-row"][data-test-id="${MUSTARD.ingredientId}"]`

const CREATED_KETCHUP = {
  ingredientId: 'ingredient-ketchup',
  name: 'Ketchup',
  unit: 'millilitre',
  isActive: true,
  activeItemNames: [],
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('adding an ingredient', () => {
  it('asks for it in an empty form headed as a new ingredient', async () => {
    ingredientsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="ingredient-row"]').exists()).toBe(true))

    await list.get('[data-test="new-ingredient"]').trigger('click')

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())
    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Neue Zutat')
    expect(inputOf('[data-test="ingredient-name-field"]').value).toBe('')
  })

  it('sends the typed name and the chosen unit and closes the form', async () => {
    const laptop = ingredientsListLaptop({ writes: answer(CREATED_KETCHUP, 201) })
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="ingredient-row"]').exists()).toBe(true))
    await list.get('[data-test="new-ingredient"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="ingredient-name-field"]')).not.toBeNull())

    await saveTheIngredientDialog(list.findComponent(IngredientDialog), 'Ketchup', 'millilitre')

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
    expect(laptop.writes()).toEqual([
      { url: '/api/admin/ingredients', method: 'POST', body: { name: 'Ketchup', unit: 'millilitre' } },
    ])
  })

  it('shows inside the form why the laptop refused the name', async () => {
    ingredientsListLaptop({ writes: refusal('errors.admin.ingredients.nameTaken', { status: 400, code: 'ValidationFailed' }) })
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="ingredient-row"]').exists()).toBe(true))
    await list.get('[data-test="new-ingredient"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="ingredient-name-field"]')).not.toBeNull())

    await saveTheIngredientDialog(list.findComponent(IngredientDialog), 'senf', 'gram')

    await vi.waitFor(() =>
      expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Eine Zutat mit diesem Namen gibt es schon.',
      ),
    )
  })

  it('names inside the form the deactivated ingredient that already has the name', async () => {
    ingredientsListLaptop({
      writes: refusal('errors.admin.ingredients.nameBelongsToDeactivatedIngredient', {
        status: 400,
        code: 'ValidationFailed',
        parameters: { name: 'Milch' },
      }),
    })
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="ingredient-row"]').exists()).toBe(true))
    await list.get('[data-test="new-ingredient"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="ingredient-name-field"]')).not.toBeNull())

    await saveTheIngredientDialog(list.findComponent(IngredientDialog), 'milch', 'millilitre')

    await vi.waitFor(() =>
      expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Die Zutat Milch gibt es schon, sie ist aber deaktiviert. Aktivieren Sie sie unter "Zutaten".',
      ),
    )
  })
})

describe('editing an ingredient', () => {
  it('opens the form headed as an edit with the name the ingredient already has', async () => {
    ingredientsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find(MUSTARD_ROW).exists()).toBe(true))

    await list.get(`${MUSTARD_ROW} [data-test="edit"]`).trigger('click')

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())
    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Zutat bearbeiten')
    expect(inputOf('[data-test="ingredient-name-field"]').value).toBe('Senf')
  })

  it('sends the changed name and unit for that ingredient', async () => {
    const laptop = ingredientsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find(MUSTARD_ROW).exists()).toBe(true))
    await list.get(`${MUSTARD_ROW} [data-test="edit"]`).trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="ingredient-name-field"]')).not.toBeNull())

    await saveTheIngredientDialog(list.findComponent(IngredientDialog), 'Senf mittelscharf', 'millilitre')

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        {
          url: '/api/admin/ingredients/ingredient-senf',
          method: 'PUT',
          body: { name: 'Senf mittelscharf', unit: 'millilitre' },
        },
      ]),
    )
  })
})
