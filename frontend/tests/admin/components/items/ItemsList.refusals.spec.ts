import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import ItemDialog from '../../../../src/admin/components/items/ItemDialog.vue'
import FestivalItems from '../../../../src/admin/components/festivals/FestivalItems.vue'
import { useAdminStationsStore } from '../../../../src/admin/stores/stations'
import { testPlugins } from '../../../support/plugins'
import { pressInDialog } from '../../../support/dom'
import { FOOD_ID, TWO_CATEGORIES, FESTIVAL_ID, ONE_ITEM, ONE_STATION, itemsListLaptop, mountList } from './itemsListFixture'
import { stubLaptop, answer, refusal } from '../../../support/laptop'
import { saveTheItemDialog } from '../../../support/formDialogs'

describe('a refusal beside an open item form', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows why the laptop kept the category while the item is being edited', async () => {
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
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))
    await list.get('[data-test="edit-item"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="item-name-field"]')).not.toBeNull())

    await list.get('[data-test="move-category-up"]').trigger('click')

    await vi.waitFor(() =>
      expect(list.get('[data-test="category-refusal"]').text()).toContain(
        'Diese Kategorie hat noch eingeschaltete Artikel.',
      ),
    )
  })

  it('shows why the laptop refused an item action while no form or dialog is open', async () => {
    itemsListLaptop({
      itemRefusal: {
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'errors.admin.actionFailed',
          parameters: {},
          details: null,
        },
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))
    await list.get('[data-test="deactivate-item"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')

    await vi.waitFor(() =>
      expect(list.get('[data-test="item-refusal"]').text()).toContain('Die Aktion ist fehlgeschlagen.'),
    )
  })

  it('leaves an item refusal to the open item form rather than the page', async () => {
    itemsListLaptop({
      itemRefusal: {
        status: 409,
        body: {
          code: 'Conflict',
          messageKey: 'errors.admin.actionFailed',
          parameters: {},
          details: null,
        },
      },
    })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))
    await list.get('[data-test="edit-item"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="item-name-field"]')).not.toBeNull())

    await saveTheItemDialog(list.findComponent(ItemDialog), 'Bratwurst', FOOD_ID)

    await vi.waitFor(() =>
      expect(list.findComponent(ItemDialog).props('errorText')).toContain('Die Aktion ist fehlgeschlagen.'),
    )
    expect(list.find('[data-test="item-refusal"]').exists()).toBe(false)
  })
})

describe('a refusal the admin has walked away from', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function laptopThatRefusesEveryChange(): void {
    stubLaptop()
      .answersEverythingElse(answer(ONE_ITEM))
      .answers('GET', '/api/admin/stations', answer(ONE_STATION))
      .answers('GET', '/api/admin/categories', answer(TWO_CATEGORIES))
      .answers('ANY', (call) => call.method !== 'GET', refusal('errors.admin.actionFailed'))
  }

  function mountFestivalItems() {
    return mount(FestivalItems, {
      props: { festivalId: FESTIVAL_ID, isRunning: false },
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
  }

  it('does not follow the admin from the item list to a festival', async () => {
    laptopThatRefusesEveryChange()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))
    await list.get('[data-test="deactivate-item"]').trigger('click')
    await pressInDialog('[data-test="confirm"]')
    await vi.waitFor(() => expect(list.find('[data-test="item-refusal"]').exists()).toBe(true))
    list.unmount()

    await useAdminStationsStore().load()
    const festival = mountFestivalItems()
    await vi.waitFor(() => expect(festival.find('[data-test="festival-item-row"]').exists()).toBe(true))

    expect(festival.find('[data-test="festival-items"] [data-test="refusal"]').exists()).toBe(false)
  })

  it('does not follow the admin from a festival to the item list', async () => {
    laptopThatRefusesEveryChange()

    await useAdminStationsStore().load()
    const festival = mountFestivalItems()
    await vi.waitFor(() => expect(festival.find('[data-test="new-item"]').exists()).toBe(true))
    await festival.get('[data-test="new-item"]').trigger('click')
    await vi.waitFor(() => expect(festival.findComponent({ name: 'ItemDialog' }).exists()).toBe(true))
    await saveTheItemDialog(festival.findComponent({ name: 'ItemDialog' }), 'Pommes', FOOD_ID)
    await vi.waitFor(() =>
      expect(festival.findComponent({ name: 'ItemDialog' }).props('errorText')).not.toBeNull(),
    )
    festival.unmount()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))

    expect(list.find('[data-test="item-refusal"]').exists()).toBe(false)
  })
})
