import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { iconActionLabelsOnScreen, isVisibleOnScreen, onScreen } from '../../../support/dom'
import { answer, neverAnswers, noConnection } from '../../../support/laptop'
import { GRAM_UNIT, MILLILITRE_UNIT, PIECE_UNIT, anAdminIngredient } from '../../../support/wireViews'
import {
  chipOf,
  chooseUnit,
  chosenUnitIds,
  mountDialog,
  namesOnTheChips,
  unitIdsOfferedForRenaming,
  unitIdsWithATrashButton,
  unitsLaptop,
} from './ingredientDialogFixture'
import { MUSTARD } from './ingredientsListFixture'

const MUSTARD_IN_A_JAR = anAdminIngredient({
  ...MUSTARD,
  unitId: 'unit-glas',
  unitName: 'Glas',
})

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('choosing the unit of an ingredient', () => {
  it('chooses the unit whose chip the admin taps', async () => {
    unitsLaptop()
    await mountDialog()

    await chooseUnit('unit-millilitre')

    expect(chosenUnitIds()).toEqual(['unit-millilitre'])
  })

  it('moves the choice to the chip tapped next', async () => {
    unitsLaptop()
    await mountDialog(MUSTARD)

    await chooseUnit('unit-millilitre')

    expect(chosenUnitIds()).toEqual(['unit-millilitre'])
  })

  it('keeps the unit chosen when its chip is tapped again', async () => {
    unitsLaptop()
    await mountDialog(MUSTARD)

    await chooseUnit('unit-gram')

    expect(chosenUnitIds()).toEqual(['unit-gram'])
  })

  it('marks the chosen chip with a check mark', async () => {
    unitsLaptop()

    await mountDialog(MUSTARD)

    expect(isVisibleOnScreen(`${chipOf('unit-gram')} .v-chip__filter .mdi-check`)).toBe(true)
    expect(isVisibleOnScreen(`${chipOf('unit-millilitre')} .v-chip__filter .mdi-check`)).toBe(false)
  })

  it('puts the pencil on the chosen chip only', async () => {
    unitsLaptop()

    await mountDialog(MUSTARD)

    expect(unitIdsOfferedForRenaming()).toEqual(['unit-gram'])
  })

  it('names the pencil Bearbeiten', async () => {
    unitsLaptop()
    await mountDialog(MUSTARD)

    onScreen(`${chipOf('unit-gram')} [data-test="rename-unit"]`).dispatchEvent(new MouseEvent('mouseenter'))

    await vi.waitFor(() => expect(iconActionLabelsOnScreen()).toContain('Bearbeiten'))
  })

  it('moves the pencil with the choice', async () => {
    unitsLaptop()
    await mountDialog(MUSTARD)

    await chooseUnit('unit-millilitre')

    expect(unitIdsOfferedForRenaming()).toEqual(['unit-millilitre'])
  })
})

describe('the unit an ingredient already has', () => {
  it('shows it by the name the ingredient carries when the list does not hold it', async () => {
    unitsLaptop({ listed: answer({ units: [PIECE_UNIT, GRAM_UNIT, MILLILITRE_UNIT] }) })

    await mountDialog(MUSTARD_IN_A_JAR)

    expect(namesOnTheChips()).toEqual(['Stück', 'g', 'ml', 'Glas'])
    expect(chosenUnitIds()).toEqual(['unit-glas'])
  })

  it('shows it before the list of units has arrived', async () => {
    unitsLaptop({ listed: neverAnswers() })

    await mountDialog(MUSTARD, { loadsTheUnitsFirst: false })

    expect(namesOnTheChips()).toEqual(['g'])
    expect(chosenUnitIds()).toEqual(['unit-gram'])
  })

  it('shows it after the list of units failed to load', async () => {
    unitsLaptop({ listed: noConnection() })

    await mountDialog(MUSTARD)

    expect(namesOnTheChips()).toEqual(['g'])
    expect(chosenUnitIds()).toEqual(['unit-gram'])
  })

  it('shows no trash button on it while the list does not say who uses it', async () => {
    unitsLaptop({ listed: noConnection() })

    await mountDialog(MUSTARD)

    expect(unitIdsWithATrashButton()).toEqual([])
  })

  it('still offers a new unit after the list of units failed to load', async () => {
    unitsLaptop({ listed: noConnection() })

    await mountDialog()

    expect(onScreen('[data-test="new-unit"]').textContent?.trim()).toBe('Neue Einheit')
  })
})
