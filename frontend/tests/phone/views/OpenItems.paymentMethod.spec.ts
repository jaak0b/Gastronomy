import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { ONE_FREE_ITEM, SETTLED, mountScreen, openItemsLaptop } from './openItemsFixture'
import { answer } from '../../support/laptop'
import { clickOn } from '../../support/dom'
import { nextTick } from 'vue'

enableAutoUnmount(afterEach)

const AMOUNT_DIALOG = '[data-test="amount-paid-dialog"]'

describe('saying how the table paid on the amount page', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends that nothing was paid when only free items are ticked', async () => {
    const laptop = openItemsLaptop(ONE_FREE_ITEM, answer(SETTLED))
    const screen = await mountScreen()
    useOpenItemsStore().toggleItem('item-water')
    await nextTick()
    await screen.get('[data-test="settle"]').trigger('click')
    await flushPromises()

    await clickOn(`${AMOUNT_DIALOG} [data-test="confirm-nothing-paid"]`)

    expect(laptop.writtenBodies()).toEqual([
      {
        lines: [{ orderItemId: 'item-water', paidPriceCents: 0, paymentNotice: null }],
        paymentMethod: 'none',
      },
    ])
  })

  it('sends card when the waiter taps card on the amount page', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreen()
    useOpenItemsStore().toggleItem('item-1')
    await nextTick()
    await screen.get('[data-test="settle"]').trigger('click')
    await flushPromises()

    await clickOn(`${AMOUNT_DIALOG} [data-test="confirm-by-card"]`)

    expect(laptop.writtenBodies()).toEqual([
      {
        lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
        paymentMethod: 'card',
      },
    ])
  })
})
