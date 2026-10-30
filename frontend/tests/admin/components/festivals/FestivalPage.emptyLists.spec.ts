import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { flushPromises } from '@vue/test-utils'
import { VAutocomplete } from 'vuetify/components'
import { useAdminItemsStore } from '../../../../src/admin/stores/items'
import { useAdminStationsStore } from '../../../../src/admin/stores/stations'
import { aHold, answer, heldUntil, noConnection } from '../../../support/laptop'
import { BAR, BEER, FESTIVAL_ID, KITCHEN, SAUSAGE, festivalLaptop, mountPage } from './festivalPageFixture'
import { OLD_TABLES } from './festivalTablePlanFixture'

const EMPTY_TEXT = '[data-test="empty-list-text"]'

beforeEach(() => {
  setActivePinia(createPinia())
  window.history.replaceState({}, '', `/admin/festivals/${FESTIVAL_ID}`)
  document.body.innerHTML = ''
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the stations card of the festival page', () => {
  it('shows the shared text of an empty list while no station belongs to the festival', async () => {
    festivalLaptop({ stations: [BAR] })

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.getComponent<typeof VAutocomplete>('[data-test="station-search"]').props('items')).toEqual([BAR]),
    )

    expect(page.get('[data-test="festival-stations"]').get(EMPTY_TEXT).text()).toBe('Keine Einträge')
  })

  it('shows no text of an empty list while a station belongs to the festival', async () => {
    festivalLaptop({ stations: [KITCHEN] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-station-row"]').exists()).toBe(true))

    expect(page.get('[data-test="festival-stations"]').find(EMPTY_TEXT).exists()).toBe(false)
  })

  it('shows no text of an empty list after the laptop failed to send the stations', async () => {
    festivalLaptop().answers('GET', '/api/admin/stations', noConnection())

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-stations"]').exists()).toBe(true))
    await flushPromises()

    expect(page.get('[data-test="festival-stations"]').find(EMPTY_TEXT).exists()).toBe(false)
  })
})

describe('the stations card of the festival page while the stations of this festival are on their way', () => {
  const NOT_AT_THE_FESTIVAL = { ...KITCHEN, isAtFestival: false }

  it('shows no text of an empty list, though the global stations are already here without a festival flag', async () => {
    const hold = aHold()
    festivalLaptop({ stations: [NOT_AT_THE_FESTIVAL] }).answers(
      'GET',
      (call) => call.url.includes('/api/admin/stations?festivalId='),
      heldUntil(hold.released, answer({ stations: [KITCHEN] })),
    )
    await useAdminStationsStore().load()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-stations"]').exists()).toBe(true))
    await flushPromises()

    expect(page.get('[data-test="festival-stations"]').find(EMPTY_TEXT).exists()).toBe(false)
  })

  it('shows the station once its own answer has arrived', async () => {
    const hold = aHold()
    festivalLaptop({ stations: [NOT_AT_THE_FESTIVAL] }).answers(
      'GET',
      (call) => call.url.includes('/api/admin/stations?festivalId='),
      heldUntil(hold.released, answer({ stations: [KITCHEN] })),
    )
    await useAdminStationsStore().load()
    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-stations"]').exists()).toBe(true))

    hold.release()

    await vi.waitFor(() => expect(page.find('[data-test="festival-station-row"]').exists()).toBe(true))
    expect(page.get('[data-test="festival-stations"]').find(EMPTY_TEXT).exists()).toBe(false)
  })
})

describe('the articles card of the festival page', () => {
  it('shows the shared text of an empty list while no article is on the festival menu', async () => {
    festivalLaptop({ items: [BEER] })

    const page = mountPage()
    await vi.waitFor(() =>
      expect(page.getComponent<typeof VAutocomplete>('[data-test="item-search"]').props('items')).toEqual([BEER]),
    )

    expect(page.get('[data-test="festival-items"]').get(EMPTY_TEXT).text()).toBe('Keine Einträge')
  })

  it('shows no text of an empty list while an article is on the festival menu', async () => {
    festivalLaptop({ items: [SAUSAGE] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-item-row"]').exists()).toBe(true))

    expect(page.get('[data-test="festival-items"]').find(EMPTY_TEXT).exists()).toBe(false)
  })

  it('shows no text of an empty list after the laptop failed to send the articles', async () => {
    festivalLaptop().answers('GET', /\/api\/admin\/items/, noConnection())

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-items"]').exists()).toBe(true))
    await flushPromises()

    expect(page.get('[data-test="festival-items"]').find(EMPTY_TEXT).exists()).toBe(false)
  })
})

describe('the articles card of the festival page while the articles of this festival are on their way', () => {
  const NOT_ON_THE_MENU = { ...SAUSAGE, atTheFestival: null }

  it('shows no text of an empty list, though the global articles are already here without a festival flag', async () => {
    const hold = aHold()
    festivalLaptop({ items: [NOT_ON_THE_MENU] }).answers(
      'GET',
      (call) => call.url.includes('/api/admin/items?festivalId='),
      heldUntil(hold.released, answer({ items: [SAUSAGE] })),
    )
    await useAdminItemsStore().load()

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-items"]').exists()).toBe(true))
    await flushPromises()

    expect(page.get('[data-test="festival-items"]').find(EMPTY_TEXT).exists()).toBe(false)
  })

  it('shows the article once its own answer has arrived', async () => {
    const hold = aHold()
    festivalLaptop({ items: [NOT_ON_THE_MENU] }).answers(
      'GET',
      (call) => call.url.includes('/api/admin/items?festivalId='),
      heldUntil(hold.released, answer({ items: [SAUSAGE] })),
    )
    await useAdminItemsStore().load()
    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-items"]').exists()).toBe(true))

    hold.release()

    await vi.waitFor(() => expect(page.find('[data-test="festival-item-row"]').exists()).toBe(true))
    expect(page.get('[data-test="festival-items"]').find(EMPTY_TEXT).exists()).toBe(false)
  })
})

describe('the table plan card of the festival page', () => {
  it('shows the shared text of an empty list while the festival has no area', async () => {
    festivalLaptop({ areas: [] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="no-area"]').exists()).toBe(true))

    expect(page.get('[data-test="festival-table-plan"]').get(EMPTY_TEXT).text()).toBe('Keine Einträge')
  })

  it('shows no text of an empty list while the only area is hidden, because the list is not empty', async () => {
    festivalLaptop({ areas: [OLD_TABLES] })

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-table-plan"]').exists()).toBe(true))
    await flushPromises()

    expect(page.get('[data-test="festival-table-plan"]').find(EMPTY_TEXT).exists()).toBe(false)
  })

  it('shows no text of an empty list before the laptop has sent the plan', async () => {
    festivalLaptop().answers('GET', /\/table-plan$/, noConnection())

    const page = mountPage()
    await vi.waitFor(() => expect(page.find('[data-test="festival-table-plan"]').exists()).toBe(true))
    await flushPromises()

    expect(page.get('[data-test="festival-table-plan"]').find(EMPTY_TEXT).exists()).toBe(false)
  })
})
