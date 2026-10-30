import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import CatalogPage from '../../../src/phone/views/Catalog.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { answer, stubLaptop } from '../../support/laptop'
import { CATALOG, openCategory, type MountedCatalog } from './catalogFixture'

enableAutoUnmount(afterEach)

function mountTheCatalogAfterTheLaptopSent(catalog: typeof CATALOG, loadFailed = false): MountedCatalog {
  const store = useCatalogStore()
  store.catalog = catalog
  store.hasLoaded = true
  store.loadFailed = loadFailed
  return mount(CatalogPage, { global: { plugins: testPlugins() }, attachTo: document.body })
}

describe('the text of an empty list on the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('is the shared text of an empty list when the laptop sent no category', () => {
    const view = mountTheCatalogAfterTheLaptopSent({ ...CATALOG, categories: [], items: [] })

    expect(view.get('[data-test="no-categories"]').text()).toBe('Keine Einträge')
  })

  it('stays away before the laptop has sent the catalog', () => {
    const store = useCatalogStore()
    store.catalog = { ...CATALOG, categories: [], items: [] }

    const view = mount(CatalogPage, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(view.find('[data-test="no-categories"]').exists()).toBe(false)
  })

  it('stays away after the last load failed', () => {
    const view = mountTheCatalogAfterTheLaptopSent({ ...CATALOG, categories: [], items: [] }, true)

    expect(view.find('[data-test="no-categories"]').exists()).toBe(false)
  })

  it('is the shared text of an empty list in an opened category that has no article', async () => {
    const view = mountTheCatalogAfterTheLaptopSent({ ...CATALOG, items: [] })

    await openCategory(view, 'Essen')

    expect({
      text: view.get('[data-test="no-items"]').text(),
      rows: view.findAll('[data-test="item-row"]').length,
    }).toEqual({ text: 'Keine Einträge', rows: 0 })
  })

  it('stays away in an opened category that has an article', async () => {
    const view = mountTheCatalogAfterTheLaptopSent(CATALOG)

    await openCategory(view, 'Essen')

    expect(view.find('[data-test="no-items"]').exists()).toBe(false)
  })
})
