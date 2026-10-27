import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { useOpenItemsStore } from '../../../src/phone/stores/openItems'
import { ONE_FREE_ITEM, TISCH_12, mountScreen, openItemsLaptop } from './openItemsFixture'
import { neverAnswers } from '../../support/laptop'
import { clickOn, inputOf, onScreen } from '../../support/dom'
import { nextTick } from 'vue'

enableAutoUnmount(afterEach)

const AMOUNT_DIALOG = '[data-test="amount-paid-dialog"]'

describe('the settle button under the selected total', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('shows the settle button under the selected total once something is ticked', async () => {
    openItemsLaptop()
    const screen = await mountScreen()

    useOpenItemsStore().toggleItem('item-1')
    await nextTick()

    const footer = screen.get('[data-test="settle-footer"]')
    expect({
      total: footer.get('[data-test="selected-total"]').text(),
      settle: footer.get('[data-test="settle"]').text(),
    }).toEqual({ total: 'Ausgewählt: 3,50 €', settle: 'Kassieren' })
  })

  it('shows the settle button in English when the device language is English', async () => {
    openItemsLaptop()
    const screen = await mountScreen('en')

    useOpenItemsStore().toggleItem('item-1')
    await nextTick()

    expect(screen.get('[data-test="settle"]').text()).toBe('Take payment')
  })

  it('opens the amount page with the whole selection filled in as the amount', async () => {
    openItemsLaptop()
    const screen = await mountScreen()
    useOpenItemsStore().toggleItem('item-1')
    await nextTick()

    await screen.get('[data-test="settle"]').trigger('click')
    await flushPromises()

    expect(document.querySelector(`${AMOUNT_DIALOG} [data-test="selected-total"]`)?.textContent).toContain(
      'Ausgewählt: 3,50 €',
    )
    expect(inputOf(`${AMOUNT_DIALOG} [data-test="amount-field"]`).value).toBe('3,50')
  })

  it('opens the amount page for a selection that costs nothing as well', async () => {
    openItemsLaptop(ONE_FREE_ITEM)
    const screen = await mountScreen()
    useOpenItemsStore().toggleItem('item-water')
    await nextTick()

    await screen.get('[data-test="settle"]').trigger('click')
    await flushPromises()

    expect(inputOf(`${AMOUNT_DIALOG} [data-test="amount-field"]`).value).toBe('0,00')
  })

  describe('the amount page while a settlement is on its way', () => {
    it('keeps Bar, Karte and Abbrechen shut until the laptop answers', async () => {
      openItemsLaptop(TISCH_12, neverAnswers())
      const screen = await mountScreen()
      useOpenItemsStore().toggleItem('item-1')
      await nextTick()
      await screen.get('[data-test="settle"]').trigger('click')
      await flushPromises()

      await clickOn(`${AMOUNT_DIALOG} [data-test="confirm-in-cash"]`)

      expect({
        cash: onScreen(`${AMOUNT_DIALOG} [data-test="confirm-in-cash"]`).hasAttribute('disabled'),
        card: onScreen(`${AMOUNT_DIALOG} [data-test="confirm-by-card"]`).hasAttribute('disabled'),
        cancel: onScreen(`${AMOUNT_DIALOG} [data-test="cancel"]`).hasAttribute('disabled'),
      }).toEqual({ cash: true, card: true, cancel: true })
    })
  })
})
