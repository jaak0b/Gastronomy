import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import CatalogPage from '../../../src/phone/views/Catalog.vue'
import { DRAFT_STORAGE_KEY } from '../../../src/phone/core/draftCart'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useOrderStore } from '../../../src/phone/stores/order'
import { navigate } from '../../../src/shared/router/router'
import { useConnectionStore } from '../../../src/shared/stores/connection'
import { useSessionStore } from '../../../src/shared/stores/session'
import { fireHubEvent, forgetHubEvents } from '../../support/hubConnection'
import { answer, inTurn, noConnection, stubLaptop } from '../../support/laptop'
import { testPlugins } from '../../support/plugins'
import { TISCH_3 } from '../../support/waiterPhone'
import { CATALOG, categoryButtons } from './catalogFixture'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

enableAutoUnmount(afterEach)

const FAILURE = '[data-test="catalog-load-failed"]'

function mountTheCatalog(language: 'de' | 'en' = 'de') {
  return mount(CatalogPage, { global: { plugins: testPlugins(language) }, attachTo: document.body })
}

async function aCatalogThatDoesNotLoadFirst(laterAnswers: ReturnType<typeof noConnection>[] = []) {
  useSessionStore().deviceToken = 'token-here'
  stubLaptop()
    .answersEverythingElse(answer({}))
    .answers('GET', '/api/catalog', inTurn(noConnection(), ...laterAnswers, answer(CATALOG)))
  const catalog = useCatalogStore()
  await catalog.load()
  return catalog
}

describe('the ordering screen while the catalog could not be loaded', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
    document.body.innerHTML = ''
    navigate('/')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says the articles could not be loaded and offers to reload them', async () => {
    await aCatalogThatDoesNotLoadFirst()

    const view = mountTheCatalog()
    await flushPromises()

    expect({
      sentences: view.findAll(`${FAILURE} [data-test="load-failure-text"]`).map((text) => text.text()),
      reloads: view.findAll(`${FAILURE} [data-test="load-failure-reload"]`).map((button) => button.text()),
    }).toEqual({
      sentences: ['Die Artikel konnten nicht geladen werden. Laden Sie die Seite neu.'],
      reloads: ['Neu laden'],
    })
  })

  it('says it in English', async () => {
    await aCatalogThatDoesNotLoadFirst()

    const view = mountTheCatalog('en')
    await flushPromises()

    expect({
      sentences: view.findAll(`${FAILURE} [data-test="load-failure-text"]`).map((text) => text.text()),
      reloads: view.findAll(`${FAILURE} [data-test="load-failure-reload"]`).map((button) => button.text()),
    }).toEqual({
      sentences: ['The items could not be loaded. Reload the page.'],
      reloads: ['Reload'],
    })
  })

  it('loads the catalog again when the waiter taps the reload, and the message goes away', async () => {
    await aCatalogThatDoesNotLoadFirst()
    const view = mountTheCatalog()
    await flushPromises()

    await view.find(`${FAILURE} [data-test="load-failure-reload"]`).trigger('click')
    await flushPromises()

    expect({ failure: view.find(FAILURE).exists(), categories: categoryButtons(view) }).toEqual({
      failure: false,
      categories: ['Essen', 'Getränke'],
    })
  })

  it('keeps the message while the reload fails as well', async () => {
    await aCatalogThatDoesNotLoadFirst([noConnection()])
    const view = mountTheCatalog()
    await flushPromises()

    await view.find(`${FAILURE} [data-test="load-failure-reload"]`).trigger('click')
    await flushPromises()

    expect(view.find(FAILURE).exists()).toBe(true)
  })

  it('loses the message as soon as any later load succeeds', async () => {
    const catalog = await aCatalogThatDoesNotLoadFirst()
    const view = mountTheCatalog()
    await flushPromises()
    expect(view.find(FAILURE).exists()).toBe(true)

    await catalog.load()
    await flushPromises()

    expect(view.find(FAILURE).exists()).toBe(false)
  })

  it('loses the message when the laptop reports a changed configuration', async () => {
    const catalog = await aCatalogThatDoesNotLoadFirst([noConnection()])
    catalog.listen()
    await useConnectionStore().connect({ deviceToken: 'token-here' })
    const view = mountTheCatalog()
    await flushPromises()
    expect(view.find(FAILURE).exists()).toBe(true)

    fireHubEvent('ConfigurationChanged')

    await vi.waitFor(() => expect(view.find(FAILURE).exists()).toBe(false))
  })
})

describe('the unsent order while the catalog could not be loaded', () => {
  const ORDER_LINE = { catalogItemId: 'item-wasser', note: null, stationId: 'station-bar', name: 'Wasser' }

  function aDraftOnTheScreen() {
    const order = useOrderStore()
    order.chooseTable(TISCH_3)
    order.addItem(ORDER_LINE)
    return localStorage.getItem(DRAFT_STORAGE_KEY)
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    forgetHubEvents()
    document.body.innerHTML = ''
    navigate('/')
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('stays exactly as it was stored after the failure and a failed reload', async () => {
    const draftBefore = aDraftOnTheScreen()
    await aCatalogThatDoesNotLoadFirst([noConnection()])
    const view = mountTheCatalog()
    await flushPromises()

    await view.find(`${FAILURE} [data-test="load-failure-reload"]`).trigger('click')
    await flushPromises()

    expect({ stored: localStorage.getItem(DRAFT_STORAGE_KEY), shown: view.find(FAILURE).exists() }).toEqual({
      stored: draftBefore,
      shown: true,
    })
  })

  it('stays exactly as it was stored once the reload succeeds', async () => {
    const draftBefore = aDraftOnTheScreen()
    await aCatalogThatDoesNotLoadFirst()
    const view = mountTheCatalog()
    await flushPromises()

    await view.find(`${FAILURE} [data-test="load-failure-reload"]`).trigger('click')
    await flushPromises()

    expect({ stored: localStorage.getItem(DRAFT_STORAGE_KEY), shown: view.find(FAILURE).exists() }).toEqual({
      stored: draftBefore,
      shown: false,
    })
  })
})
