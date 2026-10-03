import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ItemsList from '../../../../src/admin/components/items/ItemsList.vue'
import { testPlugins } from '../../../support/plugins'
import { mountList, itemsListLaptop } from './itemsListFixture'

describe('the ingredients button on an article', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('opens the recipe of that article in German', async () => {
    itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="edit-ingredients"]').exists()).toBe(true))
    expect(list.get('[data-test="edit-ingredients"]').text()).toBe('Zutaten bearbeiten')
    await list.get('[data-test="edit-ingredients"]').trigger('click')

    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog-title"]')?.textContent).toBe(
        'Zutaten für Bratwurst',
      ),
    )
  })

  it('is worded in English', async () => {
    itemsListLaptop()

    const list = mount(ItemsList, {
      global: { plugins: testPlugins('en') },
      attachTo: document.body,
    })
    await vi.waitFor(() => expect(list.find('[data-test="edit-ingredients"]').exists()).toBe(true))

    expect(list.get('[data-test="edit-ingredients"]').text()).toBe('Edit ingredients')
  })

  it('closes the recipe when the admin presses close', async () => {
    itemsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="edit-ingredients"]').exists()).toBe(true))
    await list.get('[data-test="edit-ingredients"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-cancel"]')).not.toBeNull())
    const close = document.querySelector('[data-test="form-cancel"]') as HTMLElement

    expect(close.textContent?.trim()).toBe('Schließen')
    close.click()

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
  })
})

describe('the manage ingredients button', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('opens the ingredient management in German', async () => {
    itemsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="manage-ingredients"]').exists()).toBe(true))
    expect(list.get('[data-test="manage-ingredients"]').text()).toBe('Zutaten verwalten')

    await list.get('[data-test="manage-ingredients"]').trigger('click')

    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog-title"]')?.textContent).toBe('Zutaten'),
    )
  })

  it('is worded in English', async () => {
    itemsListLaptop()

    const list = mount(ItemsList, {
      global: { plugins: testPlugins('en') },
      attachTo: document.body,
    })
    await vi.waitFor(() => expect(list.find('[data-test="manage-ingredients"]').exists()).toBe(true))

    expect(list.get('[data-test="manage-ingredients"]').text()).toBe('Manage ingredients')
  })

  it('closes the management when the admin presses close', async () => {
    itemsListLaptop()
    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="manage-ingredients"]').exists()).toBe(true))
    await list.get('[data-test="manage-ingredients"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="form-cancel"]')).not.toBeNull())

    ;(document.querySelector('[data-test="form-cancel"]') as HTMLElement).click()

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
  })
})
