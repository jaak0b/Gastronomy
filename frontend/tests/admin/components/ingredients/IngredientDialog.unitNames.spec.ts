import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { inputOf, onScreen, waitUntil } from '../../../support/dom'
import { cancelTheFormDialog, saveTheUnitDialog } from '../../../support/formDialogs'
import { answer, inTurn, noConnection, refusal } from '../../../support/laptop'
import { GRAM_UNIT, KISTE_UNIT, MILLILITRE_UNIT, PIECE_UNIT, anAdminUnit } from '../../../support/wireViews'
import {
  LISTED_UNITS,
  chipOf,
  chosenUnitIds,
  mountDialog,
  namesOnTheChips,
  openTheUnitFormToCreate,
  openTheUnitFormToRename,
  pressTheKeyOn,
  theUnitForm,
  unitFormIsOpen,
  unitsLaptop,
} from './ingredientDialogFixture'
import { MUSTARD } from './ingredientsListFixture'

const GRAMM = anAdminUnit({ ...GRAM_UNIT, name: 'Gramm' })

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('renaming the chosen unit', () => {
  it('opens the form of the unit headed as an edit with its name', async () => {
    unitsLaptop()
    await mountDialog(MUSTARD)

    await openTheUnitFormToRename('unit-gram')

    expect(onScreen('[data-test="form-dialog-title"]', theUnitForm()).textContent).toBe('Einheit bearbeiten')
    expect(inputOf('[data-test="unit-name-field"]').value).toBe('g')
  })

  it.each([['Enter'], [' ']] as const)('opens the form of the unit when the admin presses %j on the pencil', async (key) => {
    unitsLaptop()
    await mountDialog(MUSTARD)

    await pressTheKeyOn(`${chipOf('unit-gram')} [data-test="rename-unit"]`, key)

    expect(unitFormIsOpen()).toBe(true)
  })

  it('renames the unit at once and leaves the ingredient unsaved', async () => {
    const laptop = unitsLaptop()
    const dialog = await mountDialog(MUSTARD)
    await openTheUnitFormToRename('unit-gram')

    await saveTheUnitDialog('Gramm')

    await waitUntil(() => expect(unitFormIsOpen()).toBe(false))
    expect(laptop.writes()).toEqual([{ url: '/api/admin/units/unit-gram', method: 'PUT', body: { name: 'Gramm' } }])
    expect(dialog.emitted('save')).toBeUndefined()
  })

  it('shows the new name on the chip once the laptop lists the units again', async () => {
    unitsLaptop({
      listed: inTurn(answer({ units: LISTED_UNITS }), answer({ units: [PIECE_UNIT, GRAMM, MILLILITRE_UNIT, KISTE_UNIT] })),
    })
    await mountDialog(MUSTARD)
    await openTheUnitFormToRename('unit-gram')

    await saveTheUnitDialog('Gramm')

    await waitUntil(() => expect(namesOnTheChips()).toEqual(['Stück', 'Gramm', 'ml', 'Kiste']))
    expect(chosenUnitIds()).toEqual(['unit-gram'])
  })

  it('renames the unit the ingredient already has after the list of units failed to load', async () => {
    const laptop = unitsLaptop({ listed: noConnection() })
    await mountDialog(MUSTARD)
    await openTheUnitFormToRename('unit-gram')

    await saveTheUnitDialog('Gramm')

    await waitUntil(() =>
      expect(laptop.writes()).toEqual([{ url: '/api/admin/units/unit-gram', method: 'PUT', body: { name: 'Gramm' } }]),
    )
  })

  it('shows inside the form of the unit why the laptop refused the name', async () => {
    unitsLaptop({
      writes: refusal('errors.admin.units.nameTaken', { status: 400, code: 'ValidationFailed', parameters: { name: 'ml' } }),
    })
    await mountDialog(MUSTARD)
    await openTheUnitFormToRename('unit-gram')

    await saveTheUnitDialog('ml')

    await waitUntil(() =>
      expect(onScreen('[data-test="refusal"]', theUnitForm()).textContent?.trim()).toBe(
        'Die Einheit ml ist bereits vorhanden. Wählen Sie einen anderen Namen.',
      ),
    )
  })

  it('sends nothing when the admin cancels the form of the unit', async () => {
    const laptop = unitsLaptop()
    await mountDialog(MUSTARD)
    await openTheUnitFormToRename('unit-gram')

    await cancelTheFormDialog(theUnitForm())

    await waitUntil(() => expect(unitFormIsOpen()).toBe(false))
    expect(laptop.writes()).toEqual([])
  })
})

describe('creating a unit from the form for an ingredient', () => {
  it('opens an empty form headed as a new unit', async () => {
    unitsLaptop()
    await mountDialog()

    await openTheUnitFormToCreate()

    expect(onScreen('[data-test="form-dialog-title"]', theUnitForm()).textContent).toBe('Neue Einheit')
    expect(inputOf('[data-test="unit-name-field"]').value).toBe('')
  })

  it('sends the name as it was typed', async () => {
    const laptop = unitsLaptop({
      listed: answer({ units: [PIECE_UNIT, GRAM_UNIT, MILLILITRE_UNIT] }),
      writes: answer(KISTE_UNIT, 201),
    })
    await mountDialog()
    await openTheUnitFormToCreate()

    await saveTheUnitDialog('Kiste')

    await waitUntil(() => expect(laptop.writes()).toEqual([{ url: '/api/admin/units', method: 'POST', body: { name: 'Kiste' } }]))
  })

  it('shows the new unit and chooses it once the laptop created it', async () => {
    unitsLaptop({
      listed: answer({ units: [PIECE_UNIT, GRAM_UNIT, MILLILITRE_UNIT] }),
      writes: answer(KISTE_UNIT, 201),
    })
    await mountDialog(MUSTARD)
    await openTheUnitFormToCreate()

    await saveTheUnitDialog('Kiste')

    await waitUntil(() => expect(unitFormIsOpen()).toBe(false))
    expect(namesOnTheChips()).toEqual(['Stück', 'g', 'ml', 'Kiste'])
    expect(chosenUnitIds()).toEqual(['unit-kiste'])
  })

  it('shows inside the form of the unit why the laptop refused the name and chooses nothing', async () => {
    unitsLaptop({
      writes: refusal('errors.admin.units.nameTaken', { status: 400, code: 'ValidationFailed', parameters: { name: 'Kiste' } }),
    })
    await mountDialog()
    await openTheUnitFormToCreate()

    await saveTheUnitDialog('Kiste')

    await waitUntil(() =>
      expect(onScreen('[data-test="refusal"]', theUnitForm()).textContent?.trim()).toBe(
        'Die Einheit Kiste ist bereits vorhanden. Wählen Sie einen anderen Namen.',
      ),
    )
    expect(chosenUnitIds()).toEqual([])
  })
})
