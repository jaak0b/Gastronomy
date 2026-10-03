import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import CategoryDialog from '../../../../src/admin/components/categories/CategoryDialog.vue'
import { pressInDialog, waitForDialog } from '../../../support/dom'
import { FOOD_ID, TWO_CATEGORIES, mountList, itemsListLaptop } from './itemsListFixture'
import { saveTheCategoryDialog } from '../../../support/formDialogs'

describe('the controls beside a category name', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('offer renaming, both directions and switching off', async () => {
    itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="category-section"]').exists()).toBe(true))

    const heading = list.get('[data-test="category-section"]')

    expect(heading.find('[data-test="rename-category"]').exists()).toBe(true)
    expect(heading.find('[data-test="move-category-up"]').exists()).toBe(true)
    expect(heading.find('[data-test="move-category-down"]').exists()).toBe(true)
    expect(heading.find('[data-test="deactivate-category"]').exists()).toBe(true)
  })

  it('move the category up at its own address', async () => {
    const laptop = itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="category-section"]').exists()).toBe(true))
    await list.get('[data-test="move-category-up"]').trigger('click')

    await vi.waitFor(() =>
      expect(laptop.calls).toContainEqual({
        url: `/api/admin/categories/${FOOD_ID}/move`,
        method: 'POST',
        body: { direction: 'up' },
      }),
    )
  })

  it('move the category down at its own address', async () => {
    const laptop = itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="category-section"]').exists()).toBe(true))
    await list.get('[data-test="move-category-down"]').trigger('click')

    await vi.waitFor(() =>
      expect(laptop.calls).toContainEqual({
        url: `/api/admin/categories/${FOOD_ID}/move`,
        method: 'POST',
        body: { direction: 'down' },
      }),
    )
  })

  it('send the new name and colour when the category is renamed', async () => {
    const laptop = itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="category-section"]').exists()).toBe(true))
    await list.get('[data-test="rename-category"]').trigger('click')

    await saveTheCategoryDialog('Kaffee', '#6d4c41')

    await vi.waitFor(() =>
      expect(laptop.calls).toContainEqual({
        url: `/api/admin/categories/${FOOD_ID}`,
        method: 'PUT',
        body: { name: 'Kaffee', colourHex: '#6D4C41' },
      }),
    )
  })

  it('ask before the category is switched off', async () => {
    const laptop = itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="category-section"]').exists()).toBe(true))
    await list.get('[data-test="deactivate-category"]').trigger('click')

    await waitForDialog()

    expect(laptop.urls().some((url) => url.endsWith('/deactivate'))).toBe(false)
  })

  it('switch the category off once the question is answered with yes', async () => {
    const laptop = itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="category-section"]').exists()).toBe(true))
    await list.get('[data-test="deactivate-category"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(laptop.urls()).toContain(`/api/admin/categories/${FOOD_ID}/deactivate`),
    )
  })

  it('say why the laptop kept the category switched on', async () => {
    itemsListLaptop({
      refusal: {
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'errors.admin.categories.hasActiveItems',
          parameters: {},
          details: null,
        },
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="category-section"]').exists()).toBe(true))
    await list.get('[data-test="deactivate-category"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(list.get('[data-test="category-refusal"]').text()).toContain(
        'Diese Kategorie hat noch eingeschaltete Artikel.',
      ),
    )
  })
})

describe('a category that is switched off', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('stays on the page and says it is switched off, so moving stays predictable', async () => {
    itemsListLaptop({
      categories: { categories: [{ ...TWO_CATEGORIES.categories[0], isActive: false }] },
      items: { items: [] },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="category-section"]').exists()).toBe(true))

    expect(list.get('[data-test="category-name"]').text()).toBe('Speisen')
    expect(list.get('[data-test="category-section"] [data-test="category-deactivated"]').text()).toBe('Deaktiviert')
  })

  it('offers to switch it on again without asking a question first', async () => {
    const laptop = itemsListLaptop({
      categories: { categories: [{ ...TWO_CATEGORIES.categories[0], isActive: false }] },
      items: { items: [] },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="category-section"]').exists()).toBe(true))
    await list.get('[data-test="activate-category"]').trigger('click')

    await vi.waitFor(() =>
      expect(laptop.urls()).toContain(`/api/admin/categories/${FOOD_ID}/activate`),
    )
  })
})

describe('adding a category from the item list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('asks for the category in a dialog', async () => {
    itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="new-category"]').exists()).toBe(true))

    expect(document.querySelector('[data-test="form-dialog"]')).toBeNull()

    await list.get('[data-test="new-category"]').trigger('click')

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).not.toBeNull())
  })

  it('sends the name and the colour that were entered', async () => {
    const laptop = itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="new-category"]').exists()).toBe(true))
    await list.get('[data-test="new-category"]').trigger('click')
    await saveTheCategoryDialog('Nachtisch', '#6d4c41')

    await vi.waitFor(() =>
      expect(laptop.calls).toContainEqual({
        url: '/api/admin/categories',
        method: 'POST',
        body: { name: 'Nachtisch', colourHex: '#6D4C41' },
      }),
    )
  })

  it('closes the dialog once the category is saved', async () => {
    itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="new-category"]').exists()).toBe(true))
    await list.get('[data-test="new-category"]').trigger('click')
    await saveTheCategoryDialog('Nachtisch', '#6d4c41')

    await vi.waitFor(() => expect(document.querySelector('[data-test="form-dialog"]')).toBeNull())
  })

  it('keeps the dialog open and says why when the laptop refuses the name', async () => {
    itemsListLaptop({
      refusal: {
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'errors.admin.categories.nameTaken',
          parameters: {},
          details: null,
        },
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="new-category"]').exists()).toBe(true))
    await list.get('[data-test="new-category"]').trigger('click')
    await saveTheCategoryDialog('Speisen', '#6d4c41')

    await vi.waitFor(() =>
      expect(list.findComponent(CategoryDialog).props('errorText')).toBe(
        'Es gibt schon eine Kategorie mit diesem Namen. Wählen Sie einen anderen.',
      ),
    )
  })
})
