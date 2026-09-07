import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import CategoryDialog from '../../../src/components/admin/categories/CategoryDialog.vue'
import type { AdminCategory } from '../../../src/core/apiTypes'
import { testPlugins } from '../../support/plugins'

const DRINKS: AdminCategory = {
  categoryId: '11111111-1111-1111-1111-111111111111',
  name: 'Getränke',
  colourHex: '#C62828',
  sortOrder: 1,
  isActive: true,
}

function mountDialog(category: AdminCategory | null = null, errorText: string | null = null) {
  return mount(CategoryDialog, {
    props: { category, errorText },
    global: { plugins: testPlugins() },
    attachTo: document.body,
  })
}

function field(selector: string): HTMLInputElement {
  return document.querySelector(`${selector} input, input${selector}`) as HTMLInputElement
}

function typeInto(selector: string, value: string): void {
  const input = field(selector)
  input.value = value
  input.dispatchEvent(new Event('input'))
}

describe('the dialog for a category', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
  })

  it('is headed as a new category when there is none yet', async () => {
    mountDialog()
    await vi.waitFor(() => expect(document.querySelector('.category-dialog')).not.toBeNull())

    expect(document.querySelector('.category-dialog-title')!.textContent).toContain(
      'Neue Kategorie',
    )
  })

  it('is headed as a rename when a category is being changed', async () => {
    mountDialog(DRINKS)
    await vi.waitFor(() => expect(document.querySelector('.category-dialog')).not.toBeNull())

    expect(document.querySelector('.category-dialog-title')!.textContent).toContain(
      'Kategorie bearbeiten',
    )
  })

  it('starts with the name and the colour the category already has', async () => {
    mountDialog(DRINKS)
    await vi.waitFor(() => expect(document.querySelector('.category-dialog')).not.toBeNull())

    expect(field('.category-name-field').value).toBe('Getränke')
    expect(field('.category-colour-field').value).toBe('#c62828')
  })

  it('names both fields in the language of the operator', async () => {
    mountDialog()
    await vi.waitFor(() => expect(document.querySelector('.category-dialog')).not.toBeNull())

    expect(document.querySelector('.category-name-field label')!.textContent).toContain(
      'Name der Kategorie',
    )
    expect(document.querySelector('.category-colour-label')!.textContent).toContain('Farbe')
  })

  it('sends the name and the colour that were entered', async () => {
    const dialog = mountDialog()
    await vi.waitFor(() => expect(document.querySelector('.category-dialog')).not.toBeNull())

    typeInto('.category-name-field', 'Kaffee')
    typeInto('.category-colour-field', '#6d4c41')
    await dialog.vm.$nextTick()
    ;(document.querySelector('.save-category') as HTMLElement).click()
    await dialog.vm.$nextTick()

    expect(dialog.emitted('save')?.[0]?.[0]).toEqual({ name: 'Kaffee', colourHex: '#6D4C41' })
  })

  it('keeps the save button out of reach while the name is empty', async () => {
    mountDialog()
    await vi.waitFor(() => expect(document.querySelector('.category-dialog')).not.toBeNull())

    expect((document.querySelector('.save-category') as HTMLButtonElement).disabled).toBe(true)
  })

  it('shows the reason the laptop refused the category', async () => {
    mountDialog(null, 'Es gibt schon eine Kategorie mit diesem Namen.')
    await vi.waitFor(() => expect(document.querySelector('.category-dialog')).not.toBeNull())

    expect(document.querySelector('.category-dialog .refusal')!.textContent).toContain(
      'Es gibt schon eine Kategorie mit diesem Namen.',
    )
  })

  it('leaves the category alone when the admin cancels', async () => {
    const dialog = mountDialog(DRINKS)
    await vi.waitFor(() => expect(document.querySelector('.category-dialog')).not.toBeNull())

    ;(document.querySelector('.cancel-category') as HTMLElement).click()
    await dialog.vm.$nextTick()

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
    await vi.waitFor(() => expect(document.querySelector('.category-dialog')).not.toBeNull())

    expect(field('.category-name-field').getAttribute('maxlength')).toBe('200')
  })
})
