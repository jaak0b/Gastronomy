import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import { inputOf, onScreen, typeInto } from '../../../support/dom'
import { saveTheIngredientDialog } from '../../../support/formDialogs'
import {
  chooseUnit,
  chosenUnitIds,
  mountDialog,
  namesOnTheChips,
  saveIsDisabled,
  unitsLaptop,
} from './ingredientDialogFixture'
import { MUSTARD } from './ingredientsListFixture'

describe('the form for an ingredient', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is headed as a new ingredient when there is none yet', async () => {
    unitsLaptop()

    await mountDialog()

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Neue Zutat')
  })

  it('is headed as an edit when an ingredient is being changed', async () => {
    unitsLaptop()

    await mountDialog(MUSTARD)

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Zutat bearbeiten')
  })

  it('is headed in English', async () => {
    unitsLaptop()

    await mountDialog(MUSTARD, { locale: 'en' })

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Edit the ingredient')
  })

  it('starts with the name and the unit the ingredient already has', async () => {
    unitsLaptop()

    await mountDialog(MUSTARD)

    expect(inputOf('[data-test="ingredient-name-field"]').value).toBe('Senf')
    expect(chosenUnitIds()).toEqual(['unit-gram'])
  })

  it('names the name and the unit and offers every unit of the laptop in its order', async () => {
    unitsLaptop()

    await mountDialog()

    expect(onScreen('[data-test="ingredient-name-field"] label').textContent).toContain('Name der Zutat')
    expect(onScreen('[data-test="ingredient-unit-label"]').textContent?.trim()).toBe('Einheit')
    expect(namesOnTheChips()).toEqual(['Stück', 'g', 'ml', 'Kiste'])
    expect(onScreen('[data-test="new-unit"]').textContent?.trim()).toBe('Neue Einheit')
  })

  it('names the unit and the new unit in English', async () => {
    unitsLaptop()

    await mountDialog(null, { locale: 'en' })

    expect(onScreen('[data-test="ingredient-unit-label"]').textContent?.trim()).toBe('Unit')
    expect(onScreen('[data-test="new-unit"]').textContent?.trim()).toBe('New unit')
  })

  it('chooses no unit for a new ingredient', async () => {
    unitsLaptop()

    await mountDialog()

    expect(chosenUnitIds()).toEqual([])
  })

  it('keeps the save button out of reach until a unit is chosen', async () => {
    unitsLaptop()
    await mountDialog()

    typeInto('[data-test="ingredient-name-field"]', 'Ketchup')
    await nextTick()

    expect(saveIsDisabled()).toBe(true)
  })

  it('keeps the save button out of reach while the name is empty', async () => {
    unitsLaptop()
    await mountDialog()

    await chooseUnit('unit-millilitre')

    expect(saveIsDisabled()).toBe(true)
  })

  it('sends the name as it was typed together with the chosen unit', async () => {
    unitsLaptop()
    const dialog = await mountDialog()

    await saveTheIngredientDialog('Ketchup scharf', 'unit-millilitre')

    expect(dialog.emitted('save')?.[0]?.[0]).toEqual({ name: 'Ketchup scharf', unitId: 'unit-millilitre' })
  })

  it('shows the reason the laptop refused the ingredient', async () => {
    unitsLaptop()

    await mountDialog(null, {
      errorText: 'Eine Zutat mit diesem Namen ist bereits vorhanden. Wählen Sie einen anderen Namen.',
    })

    expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(
      'Eine Zutat mit diesem Namen ist bereits vorhanden. Wählen Sie einen anderen Namen.',
    )
  })
})
