import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import CatalogPage from '../../../src/phone/views/Catalog.vue'
import { useCatalogStore } from '../../../src/phone/stores/catalog'
import { useEstimatesStore } from '../../../src/phone/stores/estimates'
import { useSessionStore } from '../../../src/shared/stores/session'
import { navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { stubLaptop, answer } from '../../support/laptop'
import { MountedCatalog, openCategory, tapToAdd, CATALOG_WITH_TIMED_ITEMS, TIMED_ESTIMATES } from './catalogFixture'

enableAutoUnmount(afterEach)

describe('the waiting time on the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/')
    stubLaptop().answersEverythingElse(answer({}))
  })

  function mountCatalogWithEstimates(): MountedCatalog {
    const catalog = useCatalogStore()
    catalog.catalog = CATALOG_WITH_TIMED_ITEMS
    useEstimatesStore().items = TIMED_ESTIMATES
    return mount(CatalogPage, { global: { plugins: testPlugins() }, attachTo: document.body })
  }

  it('writes one time on an item only one station makes', async () => {
    const view = mountCatalogWithEstimates()

    await openCategory(view, 'Essen')

    expect(view.findAll('[data-test="item-row"] [data-test="name"]').map((element) => element.text())).toEqual([
      'Bratwurst',
      'Kaffee',
    ])
    expect(view.findAll('[data-test="item-row"] [data-test="estimate"]').map((element) => element.text())).toEqual([
      '~10 Min.',
      '~10 - 60 Min.',
    ])
  })

  function answerEachQuoteWith(minutesAt: Record<string, number | null>): void {
    useSessionStore().deviceToken = 'token-here'
    vi.stubGlobal(
      'fetch',
      vi.fn(async (url: string, init?: RequestInit) => {
        if (url !== '/api/estimates/quote') {
          return new Response('[]', { status: 200 })
        }
        const { lines } = JSON.parse(String(init?.body)) as {
          lines: { catalogItemId: string; stationId: string }[]
        }
        const stationId = lines.find((quoteLine) => quoteLine.catalogItemId === 'item-kaffee')?.stationId ?? ''
        return new Response(
          JSON.stringify({ stations: [{ stationId, readyInMinutes: minutesAt[stationId] ?? null }] }),
          { status: 200 },
        )
      }),
    )
  }

  async function askWhereTheKaffeeGoes(view: MountedCatalog): Promise<void> {
    await openCategory(view, 'Essen')
    await tapToAdd(view, 'Kaffee')
    await vi.waitFor(() => expect(document.querySelector('[data-test="line-station-sheet"]')).not.toBeNull())
  }

  function stationChoices(): (string | undefined)[] {
    return [...document.querySelectorAll('[data-test="station-choice"]')].map((element) =>
      element.textContent?.trim(),
    )
  }

  it('writes the time the laptop quoted for each station on its button in the station question', async () => {
    answerEachQuoteWith({ 'station-kueche': 25, 'station-bar': 70 })
    const view = mountCatalogWithEstimates()

    await askWhereTheKaffeeGoes(view)

    await vi.waitFor(() => expect(stationChoices()).toEqual(['Küche (~25 Min.)', 'Bar (~70 Min.)']))
  })
})
