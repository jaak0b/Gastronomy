import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppHeader from '../../../src/components/header/AppHeader.vue'
import { useOrderStore } from '../../../src/stores/order'
import { currentRoute, navigate } from '../../../src/router'
import { testPlugins } from '../../support/plugins'

const AppBarStub = {
  template: '<div class="app-header"><slot /></div>',
}

function mountHeader() {
  return mount(AppHeader, {
    global: {
      plugins: testPlugins(),
      stubs: { VAppBar: AppBarStub },
    },
    attachTo: document.body,
  })
}

describe('finding the way back to the ordering screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
    navigate('/orders')
  })

  it('offers the ordering screen in the row of buttons', async () => {
    const header = mountHeader()

    await header.get('.catalog-link').trigger('click')

    expect(currentRoute.value).toEqual({ name: 'home' })
  })
})

describe('the settings control', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('is named for a screen reader even though it carries an icon', () => {
    const header = mountHeader()

    expect(header.get('.settings').attributes('aria-label')).toBe('Einstellungen')
  })
})

describe('the row of destinations', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('names every destination in words, under an icon that fits a phone', () => {
    const header = mountHeader()

    expect(header.get('.catalog-link .label').text()).toBe('Bestellung aufnehmen')
    expect(header.get('.stations-link .label').text()).toBe('Ausgabestellen')
    expect(header.get('.orders-link .label').text()).toBe('Meine Bestellungen')
  })

  it('carries an icon on every destination, so the row fits without hiding a word', () => {
    const header = mountHeader()

    expect(header.get('.catalog-link .v-icon').exists()).toBe(true)
    expect(header.get('.stations-link .v-icon').exists()).toBe(true)
    expect(header.get('.orders-link .v-icon').exists()).toBe(true)
  })

  it('marks the orders that need attention on the icon rather than widening the row', async () => {
    const header = mountHeader()
    const order = useOrderStore()
    order.orders = [
      {
        orderId: 'order-1',
        globalOrderNumber: 1,
        tableName: 'Tisch 3',
        totalCents: 200,
        status: 'NeedsAttention',
        createdAtUtc: '2026-08-28T18:00:00Z',
        stationOrders: [],
      },
    ]
    await header.vm.$nextTick()

    expect(header.get('.orders-link .attention').text()).toContain('1')
  })

  it('keeps the attention mark away while every order is on its way', () => {
    const header = mountHeader()

    expect(header.find('.orders-link .attention').exists()).toBe(false)
  })
})
