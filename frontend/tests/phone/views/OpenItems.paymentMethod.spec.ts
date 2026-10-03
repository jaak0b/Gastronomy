import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import OpenItems from '../../../src/phone/views/OpenItems.vue'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import { testPlugins } from '../../support/plugins'
import { OPEN_LIST, SETTLED, mountScreen, openItemsLaptop } from './openItemsFixture'
import { answer, stubLaptop, neverAnswers } from '../../support/laptop'
import { nextTick } from 'vue'

describe('saying how the table paid with the button that settles', () => {
  const ONE_FREE_ITEM = {
    tables: [
      {
        tableName: '12',
        openAmountCents: 0,
        items: [
          {
            orderItemId: 'item-water',
            orderId: 'order-1',
            globalOrderNumber: 137,
            itemName: 'Leitungswasser',
            note: null,
            unitPriceCents: 0,
            orderedAtUtc: '2026-09-05T18:00:00Z',
          },
        ],
      },
    ],
    itemsWithoutAnOrderCount: 0,
  }

  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function mountScreenIn(language: 'de' | 'en') {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
    const session = useSessionStore()
    session.deviceToken = 'token-here'
    session.language = language
    const screen = mount(OpenItems, {
      global: { plugins: testPlugins(language) },
      attachTo: document.body,
    })
    await flushPromises()
    return screen
  }

  function footerButtons(screen: Awaited<ReturnType<typeof mountScreen>>): string[] {
    return screen.get('[data-test="settle-footer"]').findAll('button').map((button) => button.text())
  }

  it('offers cash, card and another amount once something with a price is ticked', async () => {
    openItemsLaptop()
    const screen = await mountScreenIn('de')
    useOpenItemsStore().toggleItem('item-1')
    await nextTick()

    expect(footerButtons(screen)).toEqual([
      'Bar abrechnen',
      'Mit Karte abrechnen',
      'Anderen Betrag abrechnen',
    ])
  })

  it('offers the same buttons in English', async () => {
    openItemsLaptop()
    const screen = await mountScreenIn('en')
    useOpenItemsStore().toggleItem('item-1')
    await nextTick()

    expect(footerButtons(screen)).toEqual([
      'Settle in cash',
      'Settle by card',
      'Settle a different amount',
    ])
  })

  it('settles at full price by card when the waiter taps card', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreenIn('de')
    useOpenItemsStore().toggleItem('item-1')
    await nextTick()

    await screen.get('[data-test="settle-by-card"]').trigger('click')
    await flushPromises()

    expect(laptop.writtenBodies()).toEqual([
      {
        lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
        paymentMethod: 'card',
      },
    ])
  })

  it('offers one settle button that says nothing was paid when only free items are ticked', async () => {
    const laptop = openItemsLaptop(
      ONE_FREE_ITEM,
      answer(SETTLED),
    )
    const screen = await mountScreenIn('de')
    useOpenItemsStore().toggleItem('item-water')
    await nextTick()

    expect(footerButtons(screen)).toEqual(['Abrechnen', 'Anderen Betrag abrechnen'])
    await screen.get('[data-test="settle-nothing-paid"]').trigger('click')
    await flushPromises()

    expect(laptop.writtenBodies()).toEqual([
      {
        lines: [{ orderItemId: 'item-water', paidPriceCents: 0, paymentNotice: null }],
        paymentMethod: 'none',
      },
    ])
  })

  it('keeps every settle button shut while a settlement is on its way', async () => {
    openItemsLaptop(OPEN_LIST, answer(SETTLED))
    const screen = await mountScreenIn('de')
    stubLaptop().answersEverythingElse(neverAnswers())
    useOpenItemsStore().toggleItem('item-1')
    await nextTick()

    await screen.get('[data-test="settle-in-cash"]').trigger('click')
    await nextTick()

    const shut = screen
      .get('[data-test="settle-footer"]')
      .findAll('button')
      .map((button) => button.attributes('disabled'))
    expect(shut).toEqual(['', '', ''])
  })

  it('sends card from the amount dialog when the waiter taps card there', async () => {
    const laptop = openItemsLaptop()
    const screen = await mountScreenIn('de')
    useOpenItemsStore().toggleItem('item-1')
    await nextTick()
    await screen.get('[data-test="settle-amount-paid"]').trigger('click')
    await flushPromises()

    ;(document.querySelector('[data-test="amount-paid-dialog"] [data-test="confirm-by-card"]') as HTMLElement).click()
    await flushPromises()

    expect(laptop.writtenBodies()).toEqual([
      {
        lines: [{ orderItemId: 'item-1', paidPriceCents: 350, paymentNotice: null }],
        paymentMethod: 'card',
      },
    ])
  })
})
