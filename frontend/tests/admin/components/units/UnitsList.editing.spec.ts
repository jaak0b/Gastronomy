import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { inputOf, onScreen } from '../../../support/dom'
import { saveTheUnitDialog } from '../../../support/formDialogs'
import { answer, refusal } from '../../../support/laptop'
import { anAdminUnit } from '../../../support/wireViews'
import { GRAM_UNIT, unitsListLaptop, mountList } from './unitsListFixture'

const GRAM_ROW = `[data-test="unit-row"][data-test-id="${GRAM_UNIT.unitId}"]`

const CREATED_KISTE = anAdminUnit({ unitId: 'unit-kiste', name: 'Kiste' })

beforeEach(() => {
  setActivePinia(createPinia())
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('adding a unit', () => {
  it('asks for it in an empty form headed as a new unit', async () => {
    unitsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="unit-row"]').exists()).toBe(true))

    await list.get('[data-test="new-unit"]').trigger('click')

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())
    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Neue Einheit')
    expect(inputOf('[data-test="unit-name-field"]').value).toBe('')
  })

  it('sends the typed name and closes the form', async () => {
    const laptop = unitsListLaptop({ writes: answer(CREATED_KISTE, 201) })
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="unit-row"]').exists()).toBe(true))
    await list.get('[data-test="new-unit"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="unit-name-field"]')).not.toBeNull())

    await saveTheUnitDialog('Kiste')

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
    expect(laptop.writes()).toEqual([
      { url: '/api/admin/units', method: 'POST', body: { name: 'Kiste' } },
    ])
  })

  it('shows inside the form why the laptop refused the name', async () => {
    unitsListLaptop({
      writes: refusal('errors.admin.units.nameTaken', {
        status: 400,
        code: 'ValidationFailed',
        parameters: { name: 'Kiste' },
      }),
    })
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="unit-row"]').exists()).toBe(true))
    await list.get('[data-test="new-unit"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="unit-name-field"]')).not.toBeNull())

    await saveTheUnitDialog('Kiste')

    await vi.waitFor(() =>
      expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Eine Einheit mit dem Namen Kiste gibt es bereits.',
      ),
    )
  })

  it('names inside the form the deactivated unit that already has the name', async () => {
    unitsListLaptop({
      writes: refusal('errors.admin.units.nameBelongsToDeactivatedUnit', {
        status: 400,
        code: 'ValidationFailed',
        parameters: { name: 'kg' },
      }),
    })
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="unit-row"]').exists()).toBe(true))
    await list.get('[data-test="new-unit"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="unit-name-field"]')).not.toBeNull())

    await saveTheUnitDialog('kg')

    await vi.waitFor(() =>
      expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(
        'Die Einheit kg ist deaktiviert. Aktivieren Sie sie, oder wählen Sie einen anderen Namen.',
      ),
    )
  })
})

describe('editing a unit', () => {
  it('opens the form headed as an edit with the name the unit already has', async () => {
    unitsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find(GRAM_ROW).exists()).toBe(true))

    await list.get(`${GRAM_ROW} [data-test="edit"]`).trigger('click')

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())
    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Einheit bearbeiten')
    expect(inputOf('[data-test="unit-name-field"]').value).toBe('g')
  })

  it('sends the changed name for that unit', async () => {
    const laptop = unitsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find(GRAM_ROW).exists()).toBe(true))
    await list.get(`${GRAM_ROW} [data-test="edit"]`).trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="unit-name-field"]')).not.toBeNull())

    await saveTheUnitDialog('Gramm')

    await vi.waitFor(() =>
      expect(laptop.writes()).toEqual([
        {
          url: '/api/admin/units/unit-gram',
          method: 'PUT',
          body: { name: 'Gramm' },
        },
      ]),
    )
  })
})
