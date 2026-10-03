import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useAdminFestivalsStore } from '../../../../src/admin/stores/festivals'
import { FESTIVAL_ID, ONE_FESTIVAL, THREE_ITEMS, mountList, itemsListLaptop, type ItemsListScenario } from './itemsListFixture'

describe('the item list', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('heads each category in the order the laptop gives, not in alphabetical order', async () => {
    itemsListLaptop({ items: THREE_ITEMS })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))

    expect(list.findAll('[data-test="category-name"]').map((element) => element.text())).toEqual([
      'Speisen',
      'Getränke',
    ])
  })

  it('sorts the items inside a category by name', async () => {
    itemsListLaptop({ items: THREE_ITEMS })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))

    expect(list.findAll('[data-test="item-row"] [data-test="item-name"]').map((element) => element.text())).toEqual([
      'Schnitzel',
      'Bier',
      'Wasser',
    ])
  })

  it('writes the category name on its colour, in lettering that stays readable', async () => {
    itemsListLaptop({ items: THREE_ITEMS })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))

    const headings = list.findAll('[data-test="category-name"]')

    expect(headings[0].attributes('style')).toContain('background-color: rgb(255, 235, 59)')
    expect(headings[0].attributes('style')).toContain('color: rgb(0, 0, 0)')
    expect(headings[1].attributes('style')).toContain('color: rgb(255, 255, 255)')
  })

  it('keeps a category that holds no items, so it can still be renamed or moved', async () => {
    itemsListLaptop({ items: { items: [] } })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="category-section"]').exists()).toBe(true))

    expect(list.findAll('[data-test="category-name"]').map((element) => element.text())).toEqual([
      'Speisen',
      'Getränke',
    ])
  })

  it('keeps the name and the buttons of an item on one line', async () => {
    itemsListLaptop()

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="item-row"]').exists()).toBe(true))

    const line = list.get('[data-test="item-row"] [data-test="item-line"]')

    expect(line.find('[data-test="item-name"]').exists()).toBe(true)
    expect(line.find('[data-test="edit-item"]').exists()).toBe(true)
    expect(line.find('[data-test="deactivate-item"]').exists()).toBe(true)
  })

  it('says the list could not be loaded when the festivals request fails', async () => {
    itemsListLaptop({ festivals: { notTheFestivals: [] } })

    const list = mountList()
    await vi.waitFor(() => expect(list.find('[data-test="admin-items"] [data-test="load-failed"]').exists()).toBe(true))

    expect(list.get('[data-test="admin-items"] [data-test="load-failed"]').text()).toContain(
      'Laden Sie die Seite neu. Die Daten konnten nicht geladen werden.',
    )
  })
})

describe('the item list while the running festival changes', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads the item list of the festival that is now running', async () => {
    const scenario: ItemsListScenario = {
      festivals: { festivals: [{ ...ONE_FESTIVAL.festivals[0], isRunning: true }] },
    }
    const laptop = itemsListLaptop(scenario)

    mountList()
    await vi.waitFor(() =>
      expect(laptop.urls()).toContain(`/api/admin/items?festivalId=${FESTIVAL_ID}`),
    )

    const SECOND_FESTIVAL_ID = '77777777-7777-4777-8777-777777777777'
    scenario.festivals = {
      festivals: [
        { ...ONE_FESTIVAL.festivals[0], isRunning: false },
        {
          ...ONE_FESTIVAL.festivals[0],
          festivalId: SECOND_FESTIVAL_ID,
          name: 'Herbstfest',
          isRunning: true,
        },
      ],
    }
    laptop.calls.length = 0

    await useAdminFestivalsStore().load()

    await vi.waitFor(() =>
      expect(laptop.urls()).toContain(`/api/admin/items?festivalId=${SECOND_FESTIVAL_ID}`),
    )
  })

  it('reads the whole item list again when no festival is running', async () => {
    const scenario: ItemsListScenario = {
      festivals: { festivals: [{ ...ONE_FESTIVAL.festivals[0], isRunning: true }] },
    }
    const laptop = itemsListLaptop(scenario)

    mountList()
    await vi.waitFor(() =>
      expect(laptop.urls()).toContain(`/api/admin/items?festivalId=${FESTIVAL_ID}`),
    )

    scenario.festivals = { festivals: [{ ...ONE_FESTIVAL.festivals[0], isRunning: false }] }
    laptop.calls.length = 0

    await useAdminFestivalsStore().load()

    await vi.waitFor(() => expect(laptop.urls()).toContain('/api/admin/items'))
  })
})
