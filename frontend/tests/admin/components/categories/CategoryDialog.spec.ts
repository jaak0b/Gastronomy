import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import CategoryDialog from '../../../../src/admin/components/categories/CategoryDialog.vue'
import { AdminCategoryView } from '../../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../../support/plugins'
import { inputOf, typeInto } from '../../../support/dom'
import { nextTick } from 'vue'

const DRINKS: AdminCategoryView = {
  categoryId: '11111111-1111-1111-1111-111111111111',
  name: 'Getränke',
  colourHex: '#C62828',
  sortOrder: 1,
  isActive: true,
}

function mountDialog(category: AdminCategoryView | null = null, errorText: string | null = null) {
  return mount(CategoryDialog, {
    props: { category, errorText },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

describe('the dialog for a category', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('is headed as a new category when there is none yet', async () => {
    mountDialog()
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    expect(document.querySelector('[data-test="form-dialog-title"]')!.textContent).toContain(
      'Neue Kategorie',
    )
  })

  it('is headed as a rename when a category is being changed', async () => {
    mountDialog(DRINKS)
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    expect(document.querySelector('[data-test="form-dialog-title"]')!.textContent).toContain(
      'Kategorie bearbeiten',
    )
  })

  it('starts with the name and the colour the category already has', async () => {
    mountDialog(DRINKS)
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    expect(inputOf('[data-test="category-name-field"]').value).toBe('Getränke')
    expect(inputOf('[data-test="category-colour-field"]').value).toBe('#c62828')
  })

  it('names both fields in the language of the operator', async () => {
    mountDialog()
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    expect(document.querySelector('[data-test="category-name-field"] label')!.textContent).toContain(
      'Name der Kategorie',
    )
    expect(document.querySelector('[data-test="category-colour-label"]')!.textContent).toContain('Farbe')
  })

  it('sends the name and the colour that were entered', async () => {
    const dialog = mountDialog()
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    typeInto('[data-test="category-name-field"]', 'Kaffee')
    typeInto('[data-test="category-colour-field"]', '#6d4c41')
    await nextTick()
    ;(document.querySelector('[data-test="form-save"]') as HTMLElement).click()
    await nextTick()

    expect(dialog.emitted('save')?.[0]?.[0]).toEqual({ name: 'Kaffee', colourHex: '#6D4C41' })
  })

  it('keeps the save button out of reach while the name is empty', async () => {
    mountDialog()
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    expect((document.querySelector('[data-test="form-save"]') as HTMLButtonElement).disabled).toBe(true)
  })

  it('shows the reason the laptop refused the category', async () => {
    mountDialog(null, 'Es gibt schon eine Kategorie mit diesem Namen.')
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    expect(document.querySelector('[data-test="form-dialog"] [data-test="refusal"]')!.textContent).toContain(
      'Es gibt schon eine Kategorie mit diesem Namen.',
    )
  })

  it('leaves the category alone when the admin cancels', async () => {
    const dialog = mountDialog(DRINKS)
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    ;(document.querySelector('[data-test="form-cancel"]') as HTMLElement).click()
    await nextTick()

    expect(dialog.emitted('cancel')).toHaveLength(1)
    expect(dialog.emitted('save')).toBeUndefined()
  })
})

describe('the length of a category name', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('stops where the laptop stops storing it', async () => {
    mountDialog()
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())

    expect(inputOf('[data-test="category-name-field"]').getAttribute('maxlength')).toBe('200')
  })
})
