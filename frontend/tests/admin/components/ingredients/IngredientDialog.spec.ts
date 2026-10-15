import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { VSelect } from 'vuetify/components'
import IngredientDialog from '../../../../src/admin/components/ingredients/IngredientDialog.vue'
import type { AdminIngredientView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { inputOf, onScreen, typeInto } from '../../../support/dom'
import { saveTheIngredientDialog } from '../../../support/formDialogs'
import { MUSTARD } from './ingredientsListFixture'

async function mountDialog(
  ingredient: AdminIngredientView | null = null,
  errorText: string | null = null,
  locale: 'de' | 'en' = 'de',
) {
  const dialog = mount(IngredientDialog, {
    props: { ingredient, errorText },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
  await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())
  return dialog
}

function saveIsDisabled(): boolean {
  return (onScreen('[data-test="form-save"]') as HTMLButtonElement).disabled
}

describe('the form for an ingredient', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('is headed as a new ingredient when there is none yet', async () => {
    await mountDialog()

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Neue Zutat')
  })

  it('is headed as an edit when an ingredient is being changed', async () => {
    await mountDialog(MUSTARD)

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Zutat bearbeiten')
  })

  it('is headed in English', async () => {
    await mountDialog(MUSTARD, null, 'en')

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Edit the ingredient')
  })

  it('starts with the name and the unit the ingredient already has', async () => {
    const dialog = await mountDialog(MUSTARD)

    expect(inputOf('[data-test="ingredient-name-field"]').value).toBe('Senf')
    expect(dialog.getComponent(VSelect).props('modelValue')).toBe('gram')
  })

  it('names both fields and offers the three units in German', async () => {
    const dialog = await mountDialog()

    const offered = dialog.getComponent(VSelect).props('items') as { title: string }[]

    expect(onScreen('[data-test="ingredient-name-field"] label').textContent).toContain('Name der Zutat')
    expect(onScreen('[data-test="ingredient-unit-field"] label').textContent).toContain('Einheit')
    expect(offered.map((choice) => choice.title)).toEqual(['Stück', 'g', 'ml'])
  })

  it('keeps the save button out of reach until a unit is chosen', async () => {
    await mountDialog()

    typeInto('[data-test="ingredient-name-field"]', 'Ketchup')
    await nextTick()

    expect(saveIsDisabled()).toBe(true)
  })

  it('keeps the save button out of reach while the name is empty', async () => {
    const dialog = await mountDialog()

    await dialog.getComponent(VSelect).setValue('millilitre')

    expect(saveIsDisabled()).toBe(true)
  })

  it('sends the name as it was typed together with the chosen unit', async () => {
    const dialog = await mountDialog()

    await saveTheIngredientDialog(dialog, 'Ketchup scharf', 'millilitre')

    expect(dialog.emitted('save')?.[0]?.[0]).toEqual({ name: 'Ketchup scharf', unit: 'millilitre' })
  })

  it('shows the reason the laptop refused the ingredient', async () => {
    await mountDialog(null, 'Eine Zutat mit diesem Namen gibt es schon.')

    expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(
      'Eine Zutat mit diesem Namen gibt es schon.',
    )
  })
})
