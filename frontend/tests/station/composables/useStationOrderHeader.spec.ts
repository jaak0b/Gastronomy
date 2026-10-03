import { beforeEach, describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h, nextTick, ref, type Ref } from 'vue'
import { useStationOrderHeader } from '../../../src/station/composables/useStationOrderHeader'
import type { StationOrderQueueView } from '../../../src/shared/api/generatedSchemas'
import { testPlugins } from '../../support/plugins'
import { textOnScreen } from '../../support/dom'
import { stationOrder } from '../../support/station'

function mountTheHeader(order: Ref<StationOrderQueueView>, locale: 'de' | 'en' = 'de') {
  return mount(
    defineComponent({
      setup() {
        const header = useStationOrderHeader(order)
        return () =>
          h('div', [
            h('span', { 'data-test': 'order-reference' }, header.orderReference.value),
            h('span', { 'data-test': 'delivery' }, header.deliveryText.value),
            h('span', { 'data-test': 'done-counter' }, header.doneCounter.value),
          ])
      },
    }),
    { global: { plugins: testPlugins(locale) }, attachTo: document.body },
  )
}

describe('the head of a station card', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  it('counts up when the laptop reports another item done', async () => {
    const order = ref(stationOrder({ itemCount: 3, fulfilledItemCount: 1 }))
    mountTheHeader(order)

    order.value = stationOrder({ itemCount: 3, fulfilledItemCount: 2 })
    await nextTick()

    expect(textOnScreen('[data-test="done-counter"]')).toBe('2 / 3 zubereitet')
  })

  it('names the order number and the station number in English', () => {
    const order = ref(stationOrder({ globalOrderNumber: 137, stationOrderNumber: 12 }))

    mountTheHeader(order, 'en')

    expect(textOnScreen('[data-test="order-reference"]')).toBe('Order 137 · No. 12 ·')
  })

  it('names an as-it-comes order individual', () => {
    const order = ref(stationOrder({ deliveryMode: 'asItComes' }))

    mountTheHeader(order, 'en')

    expect(textOnScreen('[data-test="delivery"]')).toBe('Individual')
  })
})
