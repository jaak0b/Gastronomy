import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppNotices from '../../../src/components/header/AppNotices.vue'
import { useConnectionStore } from '../../../src/stores/connection'
import { useOrderStore } from '../../../src/stores/order'
import { testPlugins } from '../../support/plugins'

function mountNotices() {
  return mount(AppNotices, { global: { plugins: testPlugins() }, attachTo: document.body })
}

describe('the notices above the screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = ''
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('states that the laptop cannot be reached', async () => {
    const notices = mountNotices()
    const connection = useConnectionStore()
    connection.state = 'offline'
    await notices.vm.$nextTick()

    expect(notices.get('.connection').text()).toBe(
      'Keine Verbindung zum Laptop. Es wird weiter versucht.',
    )
  })

  it('says nothing at all once the laptop answers', async () => {
    const notices = mountNotices()
    const connection = useConnectionStore()
    connection.state = 'connected'
    await notices.vm.$nextTick()

    expect(notices.find('.connection').exists()).toBe(false)
  })

  it('carries the arrival of an order onto the ordering screen', async () => {
    const notices = mountNotices()
    const order = useOrderStore()
    order.sendState = 'accepted'
    order.acceptedOrderNumber = 1
    await notices.vm.$nextTick()

    expect(notices.get('.sent').text()).toBe('Bestellung 1 ist angekommen.')
  })
})
