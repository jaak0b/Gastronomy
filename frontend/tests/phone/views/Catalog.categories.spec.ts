import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import CatalogPage from '../../../src/phone/views/Catalog.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'
import { navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { stubLaptop, answer } from '../../support/laptop'
import { nextTick } from 'vue'
import { CATALOG, mountCatalog, categoryButtons, openCategory, tapToAdd, goBackToTheCategories } from './catalogFixture'

enableAutoUnmount(afterEach)

describe('the categories on the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('lays the category buttons out in a grid of their own', () => {
    const view = mountCatalog()

    const grid = view.get('[data-test="category-grid"]')
    expect(grid.findAll('[data-test="category-button"]')).toHaveLength(2)
    view.findAll('[data-test="category-button"]').forEach((button) => {
      expect(button.element.parentElement).toBe(grid.element)
    })
  })

  it('offers every category as a button of its own', () => {
    const view = mountCatalog()

    expect(categoryButtons(view)).toEqual(['Essen', 'Getränke'])
  })

  it('shows no item at all until the waiter has picked a category', () => {
    const view = mountCatalog()

    expect(view.findAll('[data-test="item-row"]')).toHaveLength(0)
  })

  it('docks the basket bar at the bottom, so a long list of categories never hides it', () => {
    const view = mountCatalog()

    expect(view.get('[data-test="to-review"]').element.closest('[data-test="docked-strip"]')).not.toBeNull()
  })

  it('keeps the table and the summary together in one tray at the bottom', () => {
    const view = mountCatalog()

    const tray = view.get('[data-test="docked-strip"]')
    expect(tray.find('[data-test="table-field"]').exists()).toBe(true)
    expect(tray.find('[data-test="to-review"]').exists()).toBe(true)
  })

  it('keeps the order the laptop gives the categories in', () => {
    const catalog = useCatalogStore()
    catalog.catalog = { ...CATALOG, categories: [...CATALOG.categories].reverse() }
    const view = mount(CatalogPage, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(categoryButtons(view)).toEqual(['Getränke', 'Essen'])
  })

  it('paints every button in the colour the admin chose for that category', () => {
    const view = mountCatalog()

    const painted = view
      .findAll('[data-test="category-button"]')
      .map((element) => (element.element as HTMLElement).style.backgroundColor)

    expect(painted).toEqual(['rgb(255, 235, 59)', 'rgb(198, 40, 40)'])
  })

  it('writes on each button in the lettering colour that stays readable on it', () => {
    const view = mountCatalog()

    const lettering = view
      .findAll('[data-test="category-button"]')
      .map((element) => (element.element as HTMLElement).style.color)

    expect(lettering).toEqual(['rgb(0, 0, 0)', 'rgb(255, 255, 255)'])
  })

  it('shows no category and no item while the laptop still holds no menu', () => {
    const catalog = useCatalogStore()
    catalog.catalog = { ...CATALOG, categories: [], items: [] }
    const view = mount(CatalogPage, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(view.findAll('[data-test="category-button"]')).toHaveLength(0)
    expect(view.findAll('[data-test="item-row"]')).toHaveLength(0)
  })
})

describe('the items of one category on the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('shows that category and its items once its button is tapped', async () => {
    const view = mountCatalog()

    await openCategory(view, 'Getränke')

    expect(view.get('[data-test="open-category-name"]').text()).toBe('Getränke')
    expect(view.findAll('[data-test="item-row"] [data-test="name"]').map((element) => element.text())).toEqual(['Wasser'])
    expect(view.find('[data-test="back-to-categories"]').exists()).toBe(true)
  })

  it('puts the basket bar away while the waiter is inside a category', async () => {
    const view = mountCatalog()

    await openCategory(view, 'Essen')

    expect(view.find('[data-test="basket-bar"]').exists()).toBe(false)
  })

  it('docks the way back at the bottom, so a long list of items never hides it', async () => {
    const view = mountCatalog()

    await openCategory(view, 'Essen')

    expect(view.get('[data-test="back-to-categories"]').element.closest('[data-test="docked-strip"]')).not.toBeNull()
  })

  it('returns to the categories, with the order intact, when the way back is tapped', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Bratwurst')

    await goBackToTheCategories(view)

    expect(order.draft.lines).toHaveLength(1)
    expect(categoryButtons(view)).toEqual(['1 x Essen', 'Getränke'])
    expect(view.findAll('[data-test="item-row"]')).toHaveLength(0)
  })

  it('keeps the portions already ordered while the waiter looks at another category', async () => {
    const view = mountCatalog()
    const order = useOrderStore()
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Bratwurst')
    await goBackToTheCategories(view)

    await openCategory(view, 'Getränke')
    await goBackToTheCategories(view)
    await openCategory(view, 'Essen')

    expect(order.draft.lines).toHaveLength(1)
    expect(view.get('[data-test="item-row"] [data-test="note-group"] [data-test="group-count"]').text()).toBe('1')
  })

  it('sends the waiter back to the categories when the open one leaves the menu', async () => {
    const view = mountCatalog()
    const catalog = useCatalogStore()
    await openCategory(view, 'Essen')

    catalog.catalog = {
      ...CATALOG,
      categories: [CATALOG.categories[1]],
      items: [CATALOG.items[1]],
    }
    await nextTick()

    expect(view.findAll('[data-test="item-row"]')).toHaveLength(0)
    expect(categoryButtons(view)).toEqual(['Getränke'])
  })
})

describe('the portions written on a category button', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('writes how many portions of that category are on the order', async () => {
    const view = mountCatalog()
    await openCategory(view, 'Essen')

    await tapToAdd(view, 'Bratwurst')
    await tapToAdd(view, 'Bratwurst')
    await goBackToTheCategories(view)

    expect(categoryButtons(view)).toEqual(['2 x Essen', 'Getränke'])
  })

  it('writes the category name on its own while nothing from it is on the order', () => {
    const view = mountCatalog()

    expect(categoryButtons(view)).toEqual(['Essen', 'Getränke'])
  })

  it('writes the name on its own again once the last portion is taken off', async () => {
    const view = mountCatalog()
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Bratwurst')

    await view.get('[data-test="item-row"] [data-test="note-group"] [data-test="group-remove"]').trigger('click')
    await goBackToTheCategories(view)

    expect(categoryButtons(view)).toEqual(['Essen', 'Getränke'])
  })

  it('keeps counting the portions of the category the waiter is not looking at', async () => {
    const view = mountCatalog()
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Bratwurst')
    await goBackToTheCategories(view)

    await openCategory(view, 'Getränke')
    await tapToAdd(view, 'Wasser')
    await goBackToTheCategories(view)

    expect(categoryButtons(view)).toEqual(['1 x Essen', '1 x Getränke'])
  })
})
