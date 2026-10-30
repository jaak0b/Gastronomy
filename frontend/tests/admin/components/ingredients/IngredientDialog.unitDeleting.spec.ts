import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import {
  clickOn,
  dialogText,
  isOnScreen,
  onScreen,
  openDialog,
  pressInDialog,
  refusalReasonsOnScreen,
  textOnScreen,
  typeInto,
  waitForDialog,
  waitUntil,
} from '../../../support/dom'
import { formDialogHolding, saveTheFormDialog } from '../../../support/formDialogs'
import { answer, inTurn, refusal, type StubbedLaptop } from '../../../support/laptop'
import { GRAM_UNIT, MILLILITRE_UNIT, PIECE_UNIT, anAdminUnit } from '../../../support/wireViews'
import {
  LISTED_UNITS,
  chipOf,
  chooseUnit,
  chosenUnitIds,
  hoverTheTrashButtonOf,
  mountDialog,
  namesOnTheChips,
  pressTheKeyOn,
  saveIsDisabled,
  unitIdsOfferedForDeleting,
  unitIdsWithATrashButton,
  unitsLaptop,
} from './ingredientDialogFixture'
import { MUSTARD } from './ingredientsListFixture'

function laptopThatDeletesKiste(): StubbedLaptop {
  return unitsLaptop({
    listed: inTurn(answer({ units: LISTED_UNITS }), answer({ units: [PIECE_UNIT, GRAM_UNIT, MILLILITRE_UNIT] })),
  })
}

function laptopThatRefusesToDelete(): StubbedLaptop {
  return unitsLaptop({
    writes: refusal('common.errors.actionFailed', { status: 409, code: 'UnitUsedByIngredients' }),
  })
}

async function askToDelete(unitId: string): Promise<void> {
  await clickOn(`${chipOf(unitId)} [data-test="delete-unit"]`)
  await waitForDialog()
}

async function tapTheGreyedTrashButtonOf(unitId: string): Promise<void> {
  // A disabled button takes no pointer events, so the browser hands the tap to its wrapper.
  await clickOn(`${chipOf(unitId)} [data-test="delete-unit-wrapper"]`)
}

async function deleteAfterConfirming(unitId: string): Promise<void> {
  await askToDelete(unitId)
  await pressInDialog('[data-test="confirm"]')
}

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('deleting a unit from the form for an ingredient', () => {
  it('shows the trash button on every unit of the list', async () => {
    unitsLaptop()

    await mountDialog()

    expect(unitIdsWithATrashButton()).toEqual(['unit-stueck', 'unit-gram', 'unit-millilitre', 'unit-kiste'])
  })

  it('offers the trash button only on the units no ingredient uses', async () => {
    unitsLaptop()

    await mountDialog()

    expect(unitIdsOfferedForDeleting()).toEqual(['unit-millilitre', 'unit-kiste'])
  })

  it('names the ingredients that still use a unit on its disabled trash button', async () => {
    unitsLaptop({ listed: answer({ units: [anAdminUnit({ ...GRAM_UNIT, usedByIngredientNames: ['Senf', 'Zucker'] })] }) })
    await mountDialog()

    await hoverTheTrashButtonOf('unit-gram')

    await waitUntil(() =>
      expect(refusalReasonsOnScreen()).toContain('Die Einheit wird noch bei den Zutaten Senf, Zucker verwendet.'),
    )
  })

  it('names the ingredients in English', async () => {
    unitsLaptop()
    await mountDialog(null, { locale: 'en' })

    await hoverTheTrashButtonOf('unit-gram')

    await waitUntil(() => expect(refusalReasonsOnScreen()).toContain('The unit is still used by the ingredients Senf.'))
  })

  it('does not choose the unit whose trash button is tapped', async () => {
    unitsLaptop()
    await mountDialog()

    await askToDelete('unit-millilitre')

    expect(chosenUnitIds()).toEqual([])
  })

  it('keeps the chosen unit when the admin taps the greyed trash button of a unit in use', async () => {
    unitsLaptop()
    await mountDialog(MUSTARD)

    await tapTheGreyedTrashButtonOf('unit-stueck')

    expect(chosenUnitIds()).toEqual(['unit-gram'])
  })

  it.each([['Enter'], [' ']] as const)(
    'asks for confirmation when the admin presses %j on the trash button of a unit no ingredient uses',
    async (key) => {
      unitsLaptop()
      await mountDialog(MUSTARD)

      await pressTheKeyOn(`${chipOf('unit-kiste')} [data-test="delete-unit"]`, key)

      expect(dialogText('[data-test="confirm-title"]')).toBe('Einheit löschen?')
    },
  )

  it.each([['Enter'], [' ']] as const)(
    'keeps the chosen unit when the admin presses %j on the trash button of another unit',
    async (key) => {
      unitsLaptop()
      await mountDialog(MUSTARD)

      await pressTheKeyOn(`${chipOf('unit-kiste')} [data-test="delete-unit"]`, key)

      expect(chosenUnitIds()).toEqual(['unit-gram'])
    },
  )

  it('lets an arrow key on the trash button walk the chips, the way it does on a chip', async () => {
    unitsLaptop()
    await mountDialog(MUSTARD)
    const trashButton = onScreen(`${chipOf('unit-kiste')} [data-test="delete-unit"]`)
    trashButton.focus()

    trashButton.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true, cancelable: true }))

    expect((document.activeElement as HTMLElement).dataset.test).toBe('unit-chip')
  })

  it('saves the unit the admin chose after a tap on the greyed trash button of a unit in use', async () => {
    unitsLaptop()
    const dialog = await mountDialog(MUSTARD)
    await tapTheGreyedTrashButtonOf('unit-stueck')

    await saveTheFormDialog(formDialogHolding('[data-test="ingredient-name-field"]'))

    expect(dialog.emitted('save')?.[0]?.[0]).toEqual({ name: 'Senf', unitId: 'unit-gram' })
  })

  it('asks first and names the unit', async () => {
    unitsLaptop()
    await mountDialog()

    await askToDelete('unit-kiste')

    expect(dialogText('[data-test="confirm-title"]')).toBe('Einheit löschen?')
    expect(dialogText('[data-test="confirm-body"]').trim()).toBe('Die Einheit Kiste wird endgültig gelöscht.')
    expect(dialogText('[data-test="confirm"]').trim()).toBe('Einheit löschen')
  })

  it('asks in English', async () => {
    unitsLaptop()
    await mountDialog(null, { locale: 'en' })

    await askToDelete('unit-kiste')

    expect(dialogText('[data-test="confirm-title"]')).toBe('Delete this unit?')
    expect(dialogText('[data-test="confirm-body"]').trim()).toBe('The unit Kiste is deleted permanently.')
    expect(dialogText('[data-test="confirm"]').trim()).toBe('Delete unit')
  })

  it('sends nothing while the question is open', async () => {
    const laptop = unitsLaptop()
    await mountDialog()

    await askToDelete('unit-kiste')

    expect(laptop.writes()).toEqual([])
  })

  it('deletes the unit for good once the admin confirms', async () => {
    const laptop = laptopThatDeletesKiste()
    await mountDialog()

    await deleteAfterConfirming('unit-kiste')

    await waitUntil(() =>
      expect(laptop.writes()).toEqual([{ url: '/api/admin/units/unit-kiste', method: 'DELETE', body: undefined }]),
    )
  })

  it('takes the chip away once the laptop lists the units again', async () => {
    laptopThatDeletesKiste()
    await mountDialog()

    await deleteAfterConfirming('unit-kiste')

    await waitUntil(() => expect(namesOnTheChips()).toEqual(['Stück', 'g', 'ml']))
  })

  it('deletes nothing when the admin cancels the question', async () => {
    const laptop = unitsLaptop()
    await mountDialog()
    await askToDelete('unit-kiste')

    await pressInDialog('[data-test="cancel"]')

    expect(openDialog()).toBeNull()
    expect(laptop.writes()).toEqual([])
  })

  it('leaves no unit chosen once the chosen unit is deleted', async () => {
    laptopThatDeletesKiste()
    await mountDialog()
    await chooseUnit('unit-kiste')

    await deleteAfterConfirming('unit-kiste')

    await waitUntil(() => expect(namesOnTheChips()).toEqual(['Stück', 'g', 'ml']))
    expect(chosenUnitIds()).toEqual([])
  })

  it('keeps the save button out of reach once the chosen unit is deleted', async () => {
    laptopThatDeletesKiste()
    await mountDialog()
    typeInto('[data-test="ingredient-name-field"]', 'Bier')
    await chooseUnit('unit-kiste')

    await deleteAfterConfirming('unit-kiste')
    await waitUntil(() => expect(namesOnTheChips()).toEqual(['Stück', 'g', 'ml']))
    await nextTick()

    expect(saveIsDisabled()).toBe(true)
  })

  it('keeps the chosen unit when another unit is deleted', async () => {
    laptopThatDeletesKiste()
    await mountDialog(MUSTARD)

    await deleteAfterConfirming('unit-kiste')

    await waitUntil(() => expect(namesOnTheChips()).toEqual(['Stück', 'g', 'ml']))
    expect(chosenUnitIds()).toEqual(['unit-gram'])
  })
})

describe('a unit the laptop refused to delete', () => {
  it('shows under the chips why the delete failed', async () => {
    laptopThatRefusesToDelete()
    await mountDialog()

    await deleteAfterConfirming('unit-kiste')

    await waitUntil(() => expect(textOnScreen('[data-test="unit-refusal"]').trim()).toBe('Die Aktion ist fehlgeschlagen.'))
  })

  it('stays chosen', async () => {
    laptopThatRefusesToDelete()
    await mountDialog()
    await chooseUnit('unit-kiste')

    await deleteAfterConfirming('unit-kiste')

    await waitUntil(() => expect(isOnScreen('[data-test="unit-refusal"]')).toBe(true))
    expect(chosenUnitIds()).toEqual(['unit-kiste'])
  })

  it('takes the refusal away as soon as the admin chooses a unit', async () => {
    laptopThatRefusesToDelete()
    await mountDialog()
    await deleteAfterConfirming('unit-kiste')
    await waitUntil(() => expect(isOnScreen('[data-test="unit-refusal"]')).toBe(true))

    await chooseUnit('unit-gram')

    expect(isOnScreen('[data-test="unit-refusal"]')).toBe(false)
  })
})
