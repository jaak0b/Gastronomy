import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { nextTick } from 'vue'
import UnitDialog from '../../../../src/admin/components/units/UnitDialog.vue'
import type { AdminUnitView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { inputOf, onScreen, typeInto } from '../../../support/dom'
import { saveTheUnitDialog } from '../../../support/formDialogs'
import { anAdminUnit } from '../../../support/wireViews'

const GRAM = anAdminUnit({ unitId: 'unit-gram', name: 'g' })

async function mountDialog(
  unit: AdminUnitView | null = null,
  errorText: string | null = null,
  locale: 'de' | 'en' = 'de',
) {
  const dialog = mount(UnitDialog, {
    props: { unit, errorText },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
  await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())
  return dialog
}

function saveIsDisabled(): boolean {
  return (onScreen('[data-test="form-save"]') as HTMLButtonElement).disabled
}

describe('the form for a unit', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('is headed as a new unit when there is none yet', async () => {
    await mountDialog()

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Neue Einheit')
  })

  it('is headed as an edit when a unit is being changed', async () => {
    await mountDialog(GRAM)

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Einheit bearbeiten')
  })

  it('is headed in English', async () => {
    await mountDialog(GRAM, null, 'en')

    expect(onScreen('[data-test="form-dialog-title"]').textContent).toBe('Edit the unit')
  })

  it('starts with the name the unit already has', async () => {
    await mountDialog(GRAM)

    expect(inputOf('[data-test="unit-name-field"]').value).toBe('g')
  })

  it('names the field and limits the name to 200 characters', async () => {
    await mountDialog()

    expect(onScreen('[data-test="unit-name-field"] label').textContent).toContain('Name der Einheit')
    expect(inputOf('[data-test="unit-name-field"]').maxLength).toBe(200)
  })

  it('keeps the save button out of reach while the name is empty', async () => {
    await mountDialog()

    typeInto('[data-test="unit-name-field"]', '   ')
    await nextTick()

    expect(saveIsDisabled()).toBe(true)
  })

  it('sends the name as it was typed', async () => {
    const dialog = await mountDialog()

    typeInto('[data-test="unit-name-field"]', 'Kiste')
    await nextTick()
    expect(saveIsDisabled()).toBe(false)
    await saveTheUnitDialog('Kiste')

    expect(dialog.emitted('save')?.[0]?.[0]).toEqual({ name: 'Kiste' })
  })

  it('shows the reason the laptop refused the unit', async () => {
    await mountDialog(null, 'Eine Einheit mit dem Namen g gibt es bereits.')

    expect(onScreen('[data-test="form-dialog"] [data-test="refusal"]').textContent?.trim()).toBe(
      'Eine Einheit mit dem Namen g gibt es bereits.',
    )
  })
})
