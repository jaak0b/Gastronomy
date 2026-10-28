import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount, type DOMWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppHeader from '../../../../src/phone/components/header/AppHeader.vue'
import { useOpenItemsStore } from '../../../../src/phone/stores/openItems'
import { useOrderOverviewStore } from '../../../../src/phone/stores/orderOverview'
import { useOrderStore } from '../../../../src/phone/stores/order'
import { useTablePlanStore } from '../../../../src/phone/stores/tablePlan'
import { currentRoute, navigate, registerOpenStepCloser } from '../../../../src/shared/router/router'
import { testPlugins } from '../../../support/plugins'
import { stubLaptop, answer } from '../../../support/laptop'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../../src/shared/stores/session'
import { aPlanArea, aPlanTable } from '../../../support/wireViews'
import { nextTick } from 'vue'

enableAutoUnmount(afterEach)

const AppBarStub = {
  template: '<div class="app-header"><slot /></div>',
}

const SEARCH = '/api/open-items/tables/table-12/search'

const HALL = aPlanArea({ areaId: 'area-halle', name: 'Halle' })

const GARDEN = aPlanArea({ areaId: 'area-garten', name: 'Garten' })

function mountHeader(locale: 'de' | 'en' = 'de') {
  return mount(AppHeader, {
    global: {
      plugins: testPlugins(locale),
      stubs: { VAppBar: AppBarStub },
    },
    attachTo: document.body,
  })
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  stubLaptop().answersEverythingElse(answer({}))
})

afterEach(() => {
  vi.unstubAllGlobals()
})

describe('the header over the plan of one area', () => {
  beforeEach(() => {
    navigate('/')
  })

  it('names the area the waiter is looking at', () => {
    const tablePlan = useTablePlanStore()
    tablePlan.areas = [HALL, GARDEN]
    tablePlan.openArea('area-garten')

    const header = mountHeader()

    expect(header.get('[data-test="header-title"]').text()).toBe('Garten')
  })

  it('leads back to the list of areas when the festival has several', async () => {
    const tablePlan = useTablePlanStore()
    tablePlan.areas = [HALL, GARDEN]
    tablePlan.openArea('area-garten')
    const header = mountHeader()
    expect(header.get('[data-test="header-back"]').text()).toBe('Tischpläne')

    await header.get('[data-test="header-back"]').trigger('click')

    expect(tablePlan.openedAreaId).toBeNull()
  })

  it('offers no way back when the festival has only the one area', () => {
    useTablePlanStore().areas = [HALL]

    const header = mountHeader()

    expect(header.find('[data-test="header-back"]').exists()).toBe(false)
  })

  it('offers no way back on the list of areas itself', () => {
    useTablePlanStore().areas = [HALL, GARDEN]

    const header = mountHeader()

    expect(header.find('[data-test="header-back"]').exists()).toBe(false)
  })
})

describe('the header over an order being taken', () => {
  beforeEach(() => {
    navigate('/catalog')
  })

  it('names the table the order is for', () => {
    useOrderStore().chooseTable({ tableId: 'table-12', name: '12' })

    const header = mountHeader()

    expect(header.get('[data-test="header-title"]').text()).toBe('Tisch 12')
  })

  it('names the table by the name the plan holds now, after the admin renamed it', () => {
    useTablePlanStore().areas = [aPlanArea({ tables: [aPlanTable({ tableId: 'table-12', name: '12a' })] })]
    useOrderStore().chooseTable({ tableId: 'table-12', name: '12' })

    const header = mountHeader()

    expect(header.get('[data-test="header-title"]').text()).toBe('Tisch 12a')
  })

  it('keeps the name the order was opened under while the table is no longer on the plan', () => {
    useTablePlanStore().areas = [aPlanArea({ tables: [] })]
    useOrderStore().chooseTable({ tableId: 'table-12', name: '12' })

    const header = mountHeader()

    expect(header.get('[data-test="header-title"]').text()).toBe('Tisch 12')
  })

  it('leads back to the table plan', async () => {
    const header = mountHeader()
    expect(header.get('[data-test="header-back"]').text()).toBe('Tischplan')

    await header.get('[data-test="header-back"]').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })

  it('names the way back in English on an English phone', () => {
    const header = mountHeader('en')

    expect(header.get('[data-test="header-back"]').text()).toBe('Table plan')
  })

  it('closes an open category on the way back, so the tap is answered instead of doing nothing', async () => {
    let timesClosed = 0
    registerOpenStepCloser(() => {
      timesClosed += 1
    })
    const header = mountHeader()

    await header.get('[data-test="header-back"]').trigger('click')

    expect(timesClosed).toBe(1)
  })
})

describe('the header over a table being settled', () => {
  beforeEach(() => {
    navigate('/open-items')
  })

  it('names the table by the name the laptop holds for it now', async () => {
    const header = mountHeader()

    useOpenItemsStore().report = { tableId: 'table-12', tableName: '12a', orders: [] }
    await nextTick()

    expect(header.get('[data-test="header-title"]').text()).toBe('Tisch 12a')
  })

  it('leads back to the table plan', async () => {
    const header = mountHeader()

    await header.get('[data-test="header-back"]').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})

describe('the header over the orders of a table', () => {
  beforeEach(() => {
    navigate('/order-overview')
  })

  it('names the table by the name the laptop holds for it now', async () => {
    const header = mountHeader()

    useOrderOverviewStore().report = { tableId: 'table-12', tableName: '12a', orders: [] }
    await nextTick()

    expect(header.get('[data-test="header-title"]').text()).toBe('Tisch 12a')
  })

  it('leads back to the table plan', async () => {
    const header = mountHeader()

    await header.get('[data-test="header-back"]').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})

function tooltipOf(button: Pick<DOMWrapper<Element>, 'getComponent'>): string | undefined {
  const overlay = button.getComponent({ name: 'VTooltip' }).getComponent({ name: 'VOverlay' })
  return (overlay.vm as unknown as { contentEl?: HTMLElement }).contentEl?.textContent ?? undefined
}

describe('the reload in the bar', () => {
  function signedIn(): void {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    useSessionStore().deviceToken = 'token-here'
  }

  it('fetches the table being settled again', async () => {
    signedIn()
    const laptop = stubLaptop().answers('POST', SEARCH, answer({ tableId: 'table-12', tableName: '12', orders: [] }))
    useOpenItemsStore().openTable('table-12')
    navigate('/open-items')
    const header = mountHeader()

    await header.get('[data-test="reload"]').trigger('click')
    await flushPromises()

    expect(laptop.callsTo('POST', SEARCH)).toHaveLength(1)
  })

  it('fetches the orders of the table again', async () => {
    signedIn()
    const laptop = stubLaptop().answers('POST', SEARCH, answer({ tableId: 'table-12', tableName: '12', orders: [] }))
    useOrderOverviewStore().openTable('table-12')
    navigate('/order-overview')
    const header = mountHeader()

    await header.get('[data-test="reload"]').trigger('click')
    await flushPromises()

    expect(laptop.callsTo('POST', SEARCH)).toHaveLength(1)
  })

  it('shows no word on the button itself', () => {
    navigate('/open-items')

    const reload = mountHeader().get('[data-test="reload"]')

    expect(reload.text()).toBe('')
  })

  it.each([
    ['de', 'Neu laden'],
    ['en', 'Reload'],
  ] as const)('names itself in a tooltip in the %s phone language', async (locale, word) => {
    navigate('/open-items')
    const reload = mountHeader(locale).get('[data-test="reload"]')

    await reload.trigger('mouseenter')

    await vi.waitFor(() => expect(tooltipOf(reload)).toBe(word))
  })

  it('is not offered over the table plan', () => {
    navigate('/')

    const header = mountHeader()

    expect(header.find('[data-test="reload"]').exists()).toBe(false)
  })

  it('is not offered over an order being taken', () => {
    navigate('/catalog')

    const header = mountHeader()

    expect(header.find('[data-test="reload"]').exists()).toBe(false)
  })
})
