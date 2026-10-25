import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import type { AdminUnitView } from '../../../../src/shared/api/generatedSchemas'
import {
  dialogText,
  pressInDialog,
  refusalReasonsOnScreen,
  waitForDialog,
} from '../../../support/dom'
import { refusal } from '../../../support/laptop'
import { OLD_KILOGRAM, GRAM_UNIT, PIECE_UNIT, unitsListLaptop, mountList } from './unitsListFixture'

const GRAM_ROW = `[data-test="unit-row"][data-test-id="${GRAM_UNIT.unitId}"]`

const PIECE_ROW = `[data-test="unit-row"][data-test-id="${PIECE_UNIT.unitId}"]`

const USED_GRAM: AdminUnitView = { ...GRAM_UNIT, usedByIngredientNames: ['Mehl', 'Zucker'] }

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('deleting a unit', () => {
  it('asks before it happens', async () => {
    const laptop = unitsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find(GRAM_ROW).exists()).toBe(true))

    await list.get(`${GRAM_ROW} [data-test="deactivate"]`).trigger('click')

    await waitForDialog()
    expect(dialogText('[data-test="confirm-title"]')).toBe('Einheit deaktivieren?')
    expect(dialogText('[data-test="confirm"]').trim()).toBe('Einheit deaktivieren')
    expect(laptop.writes()).toEqual([])
  })

  it('deactivates it once the question is answered with yes', async () => {
    const laptop = unitsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find(GRAM_ROW).exists()).toBe(true))
    await list.get(`${GRAM_ROW} [data-test="deactivate"]`).trigger('click')

    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/units/unit-gram/deactivate',
      ]),
    )
  })

  it('does nothing when the question is answered with no', async () => {
    const laptop = unitsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find(GRAM_ROW).exists()).toBe(true))
    await list.get(`${GRAM_ROW} [data-test="deactivate"]`).trigger('click')

    await pressInDialog('[data-test="cancel"]')

    expect(laptop.writes()).toEqual([])
  })

  it('shows on the page why the laptop refused it', async () => {
    unitsListLaptop({ writes: refusal('errors.admin.actionFailed') })
    const list = mountList()
    await vi.waitFor(() => expect(list.find(GRAM_ROW).exists()).toBe(true))
    await list.get(`${GRAM_ROW} [data-test="deactivate"]`).trigger('click')

    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(list.get('[data-test="refusal"]').text()).toBe(
        'Die Aktion ist fehlgeschlagen. Versuchen Sie es noch einmal, sonst laden Sie die Seite neu.',
      ),
    )
  })
})

describe('a unit an ingredient still uses', () => {
  it('cannot be deleted while another unit can', async () => {
    unitsListLaptop({ units: [PIECE_UNIT, USED_GRAM] })

    const list = mountList()
    await vi.waitFor(() =>
      expect((list.get(`${GRAM_ROW} [data-test="deactivate"]`).element as HTMLButtonElement).disabled).toBe(true),
    )

    expect((list.get(`${PIECE_ROW} [data-test="deactivate"]`).element as HTMLButtonElement).disabled).toBe(false)
  })

  it('names the ingredients the laptop lists for it', async () => {
    unitsListLaptop({ units: [PIECE_UNIT, USED_GRAM] })
    const list = mountList()
    await vi.waitFor(() =>
      expect((list.get(`${GRAM_ROW} [data-test="deactivate"]`).element as HTMLButtonElement).disabled).toBe(true),
    )

    await list.get(`${GRAM_ROW} [data-test="deactivate-wrapper"]`).trigger('mouseenter')

    await vi.waitFor(() =>
      expect(refusalReasonsOnScreen()).toContain(
        'Diese Einheit wird noch bei den Zutaten Mehl, Zucker verwendet. Ändern Sie bei diesen Zutaten zuerst die Einheit, unter "Zutaten".',
      ),
    )
  })

  it('names the ingredients in English', async () => {
    unitsListLaptop({ units: [PIECE_UNIT, USED_GRAM] })
    const list = mountList('en')
    await vi.waitFor(() =>
      expect((list.get(`${GRAM_ROW} [data-test="deactivate"]`).element as HTMLButtonElement).disabled).toBe(true),
    )

    await list.get(`${GRAM_ROW} [data-test="deactivate-wrapper"]`).trigger('mouseenter')

    await vi.waitFor(() =>
      expect(refusalReasonsOnScreen()).toContain(
        'This unit is still used by the ingredients Mehl, Zucker. Change the unit of these ingredients first, under "Ingredients".',
      ),
    )
  })
})

describe('a deactivated unit', () => {
  it('is activated again without a question first', async () => {
    const laptop = unitsListLaptop({ units: [OLD_KILOGRAM] })
    const list = mountList()
    await list.get('[data-test="show-deactivated"] input').setValue(true)
    await vi.waitFor(() => expect(list.find('[data-test="reactivate"]').exists()).toBe(true))

    await list.get('[data-test="reactivate"]').trigger('click')

    await vi.waitFor(() =>
      expect(laptop.writes().map((call) => `${call.method} ${call.url}`)).toEqual([
        'POST /api/admin/units/unit-kilogram/activate',
      ]),
    )
  })
})
