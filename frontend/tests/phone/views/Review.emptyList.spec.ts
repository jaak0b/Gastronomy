import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { enableAutoUnmount, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Review from '../../../src/phone/views/Review.vue'
import { useOrderStore } from '../../../src/phone/stores/order'
import { navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { TISCH_3 } from '../../support/waiterPhone'
import { prepareOrder } from './reviewFixture'

enableAutoUnmount(afterEach)

describe('the review of an order', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    navigate('/review')
  })

  it('shows the shared text of an empty list while the order has no line', () => {
    useOrderStore().chooseTable(TISCH_3)

    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(review.get('[data-test="no-lines"]').text()).toBe('Keine Einträge')
  })

  it('shows no text of an empty list while the order has a line', () => {
    prepareOrder()

    const review = mount(Review, { global: { plugins: testPlugins() }, attachTo: document.body })

    expect(review.find('[data-test="no-lines"]').exists()).toBe(false)
  })
})
