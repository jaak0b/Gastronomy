import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import CatalogPage from '../../../src/phone/views/Catalog.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'
import { navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { stubLaptop, answer } from '../../support/laptop'
import { nextTick } from 'vue'
import { mountCatalog, type MountedCatalog, openCategory, itemRowNamed, tapToAdd, stationChoiceNamed, goBackToTheCategories, CATALOG_WITH_A_STATION_CHOICE } from './catalogFixture'

enableAutoUnmount(afterEach)

describe('the question about which station is to make an item', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(answer({}))
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function mountCatalogWithAStationChoice() {
    const catalog = useCatalogStore()
    catalog.catalog = CATALOG_WITH_A_STATION_CHOICE
    return mount(CatalogPage, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  async function askWhereTheCoffeeIsMade(view: MountedCatalog): Promise<void> {
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Kaffee')
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).not.toBeNull())
  }

  it('forgets the unanswered question when the waiter leaves the category', async () => {
    const view = mountCatalogWithAStationChoice()
    const order = useOrderStore()
    await askWhereTheCoffeeIsMade(view)

    await goBackToTheCategories(view)
    await openCategory(view, 'Getränke')
    await nextTick()
    await nextTick()

    expect(document.querySelector('[data-test="line-station-sheet"]')).toBeNull()
    expect(order.draft.lines).toHaveLength(0)
  })

  it('adds nothing and stays in the category when the waiter cancels the question', async () => {
    const view = mountCatalogWithAStationChoice()
    const order = useOrderStore()
    await askWhereTheCoffeeIsMade(view)

    document.querySelector<HTMLElement>('[data-test="cancel-station-choice"]')?.click()
    await nextTick()

    expect(document.querySelector('[data-test="line-station-sheet"]')).toBeNull()
    expect(order.draft.lines).toHaveLength(0)
    expect(view.findAll('[data-test="item-row"]').length).toBeGreaterThan(0)
  })

  it('takes the note in the station sheet and puts both on the line', async () => {
    const view = mountCatalogWithAStationChoice()
    const order = useOrderStore()
    await openCategory(view, 'Essen')

    await itemRowNamed(view, 'Kaffee').get('[data-test="add-note"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).not.toBeNull())
    expect(document.querySelector('[data-test="line-station-sheet"] [data-test="station-note-input"]')).not.toBeNull()
    expect(
      document.querySelector('[data-test="line-station-sheet"] [data-test="station-where-title"]')?.textContent?.trim(),
    ).toBe('Ausgabestelle für Kaffee')

    const field = document.querySelector(
      '[data-test="line-station-sheet"] [data-test="station-note-input"] input',
    ) as HTMLInputElement
    expect(document.activeElement).toBe(field)
    field.value = 'ohne Zucker'
    field.dispatchEvent(new Event('input'))
    await nextTick()
    stationChoiceNamed('Küche').click()
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).toBeNull())

    expect(order.draft.lines).toHaveLength(1)
    expect(order.draft.lines[0].note).toBe('ohne Zucker')
    expect(order.draft.lines[0].stationId).toBe('station-kueche')
  })

  it('adds another portion to the station of that group', async () => {
    const view = mountCatalogWithAStationChoice()
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-kaffee',
      note: null,
      stationId: 'station-bar',
      name: 'Kaffee',
    })
    order.addItem({
      catalogItemId: 'item-kaffee',
      note: null,
      stationId: 'station-bar',
      name: 'Kaffee',
    })

    await openCategory(view, 'Essen')
    await itemRowNamed(view, 'Kaffee').get('[data-test="group-add"]').trigger('click')

    expect(order.draft.lines).toHaveLength(3)
    expect(order.draft.lines[2].stationId).toBe('station-bar')
    expect(order.draft.lines[2].note).toBeNull()
  })

  it('keeps the note of a line while its station is changed', async () => {
    const view = mountCatalogWithAStationChoice()
    const order = useOrderStore()
    order.addItem({
      catalogItemId: 'item-kaffee',
      note: 'ohne Zucker',
      stationId: 'station-bar',
      name: 'Kaffee',
    })

    await openCategory(view, 'Essen')
    await itemRowNamed(view, 'Kaffee').get('[data-test="group-station"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).not.toBeNull())

    expect(document.querySelector('[data-test="line-station-sheet"] [data-test="station-note-input"]')).toBeNull()

    stationChoiceNamed('Küche').click()
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).toBeNull())

    expect(order.draft.lines).toHaveLength(1)
    expect(order.draft.lines[0].note).toBe('ohne Zucker')
    expect(order.draft.lines[0].stationId).toBe('station-kueche')
  })

  it('lifts the station sheet above the keyboard while its note is typed', async () => {
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', {
      height: 400,
      scale: 1,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })
    const view = mountCatalogWithAStationChoice()

    await askWhereTheCoffeeIsMade(view)

    const overlay = document.querySelector('[data-test="line-station-overlay"]') as HTMLElement
    expect(overlay.style.height).toBe('calc(100% - 400px)')
    expect(overlay.style.bottom).toBe('auto')
    expect(overlay.classList).toContain('v-dialog--scrollable')
  })
})

describe('the ordering screen on a phone whose keyboard covers the lower screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(answer({}))
    vi.stubGlobal('innerHeight', 800)
    vi.stubGlobal('visualViewport', {
      height: 400,
      scale: 1,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lifts the tray with the table field above the keyboard', async () => {
    const view = mountCatalog()
    await nextTick()

    expect((view.get('[data-test="catalog"]').element as HTMLElement).style.paddingBottom).toBe('400px')
  })

  it('leaves the tray where it is while the waiter is zoomed in', async () => {
    vi.stubGlobal('visualViewport', {
      height: 400,
      scale: 2,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })
    const view = mountCatalog()
    await nextTick()

    expect((view.get('[data-test="catalog"]').element as HTMLElement).style.paddingBottom).toBe('0px')
  })
})
