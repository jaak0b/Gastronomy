import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { noConnection, stubLaptop } from '../../../support/laptop'
import {
  DEACTIVATED_ITEM,
  ONE_ITEM,
  TWO_CATEGORIES,
  itemsListLaptop,
  mountList,
} from './itemsListFixture'

const EMPTY_TEXT = '[data-test="empty-list-text"]'

describe('the text of an empty list on the articles tab', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is the shared text of an empty list when the laptop sent no category', async () => {
    const laptop = itemsListLaptop({ categories: { categories: [] }, items: { items: [] } })

    const list = mountList()
    await vi.waitFor(() => expect(laptop.urls()).toContain('/api/admin/categories'))
    await flushPromises()

    expect(list.findAll(EMPTY_TEXT).map((text) => text.text())).toEqual(['Keine Einträge'])
  })

  it('is the shared text of an empty list under a category that has no article', async () => {
    itemsListLaptop({ items: ONE_ITEM })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))

    expect(list.findAll(`[data-test="category-section"] ${EMPTY_TEXT}`).map((text) => text.text())).toEqual([
      'Keine Einträge',
    ])
  })

  it('stays away under a category whose articles are all deactivated, because the category is not empty', async () => {
    const laptop = itemsListLaptop({
      categories: { categories: [TWO_CATEGORIES.categories[0]] },
      items: DEACTIVATED_ITEM,
    })

    const list = mountList()
    await vi.waitFor(() => expect(laptop.urls()).toContain('/api/admin/items'))
    await flushPromises()

    expect(list.findAll(EMPTY_TEXT)).toHaveLength(0)
  })

  it('stays away after the load failed', async () => {
    stubLaptop().answersEverythingElse(noConnection())

    const list = mountList()
    await flushPromises()

    expect(list.findAll(EMPTY_TEXT)).toHaveLength(0)
  })
})
