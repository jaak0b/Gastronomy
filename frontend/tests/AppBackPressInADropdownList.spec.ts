import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import {
  aBrowserThatCountsTheTap,
  aBrowserWithoutUserActivation,
  pressBack,
  shownOnAStartedRouterAt,
  theAppWasLeftByBack,
} from './support/browserHistory'
import { isVisibleOnScreen, onScreen, waitUntil } from './support/dom'
import { answer, stubLaptop } from './support/laptop'
import { anAdminIngredient } from './support/wireViews'

enableAutoUnmount(afterEach)

const EVERY_ORDER = { orderStatus: null, paymentStatus: null, includeCancelled: false }
const AN_OPEN_LIST = '.v-select__content, .v-autocomplete__content'
const SENF = anAdminIngredient({ ingredientId: 'ingredient-senf', name: 'Senf' })

async function openTheListOf(field: string): Promise<void> {
  onScreen(`${field} .v-field`).dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
  await flushPromises()
  await waitUntil(() => expect(isVisibleOnScreen(AN_OPEN_LIST)).toBe(true))
}

function timesEmitted(shown: VueWrapper, event: string): number {
  return shown.emitted(event)?.length ?? 0
}

beforeEach(() => {
  vi.resetModules()
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  stubLaptop().answersEverythingElse(answer({ ingredients: [SENF], categories: [] }))
  aBrowserThatCountsTheTap()
})

afterEach(() => {
  aBrowserWithoutUserActivation()
})

describe('back while a list of the order filter is open', () => {
  it('closes the order status list and stays on the order overview', async () => {
    await shownOnAStartedRouterAt('/order-overview', () => import('../src/phone/components/orderList/OrderListFilter.vue'), {
      filter: EVERY_ORDER,
    })
    await openTheListOf('[data-test="order-status-filter"]')

    await pressBack()

    expect({ open: isVisibleOnScreen(AN_OPEN_LIST), leftTheApp: theAppWasLeftByBack() }).toEqual({ open: false, leftTheApp: false })
  })

  it('closes the payment status list and stays on the settle view', async () => {
    await shownOnAStartedRouterAt('/open-items', () => import('../src/phone/components/orderList/OrderListFilter.vue'), {
      filter: EVERY_ORDER,
    })
    await openTheListOf('[data-test="payment-status-filter"]')

    await pressBack()

    expect({ open: isVisibleOnScreen(AN_OPEN_LIST), leftTheApp: theAppWasLeftByBack() }).toEqual({ open: false, leftTheApp: false })
  })
})

describe('back while the language list is open', () => {
  it('closes the list and keeps the settings open', async () => {
    const settings = await shownOnAStartedRouterAt('/', () => import('../src/phone/components/header/SettingsSheet.vue'), {})
    await openTheListOf('[data-test="language-switch"]')

    await pressBack()

    expect({ open: isVisibleOnScreen(AN_OPEN_LIST), closed: timesEmitted(settings, 'close'), leftTheApp: theAppWasLeftByBack() }).toEqual({
      open: false,
      closed: 0,
      leftTheApp: false,
    })
  })
})

describe('back while a list inside an admin dialog is open', () => {
  it('closes the category list and keeps the article dialog open', async () => {
    const dialog = await shownOnAStartedRouterAt('/admin/items', () => import('../src/admin/components/items/ItemDialog.vue'), {
      item: null,
      errorText: null,
    })
    await openTheListOf('[data-test="category-field"]')

    await pressBack()

    expect({ open: isVisibleOnScreen(AN_OPEN_LIST), cancelled: timesEmitted(dialog, 'cancel'), leftTheApp: theAppWasLeftByBack() }).toEqual({
      open: false,
      cancelled: 0,
      leftTheApp: false,
    })
  })
})

describe('back while the search of an add line is open', () => {
  it('closes the search and stays on the screen', async () => {
    await shownOnAStartedRouterAt('/admin/festivals/fest-1', () => import('../src/admin/components/BaseSearchAddLine.vue'), {
      entries: [SENF],
      entryTitle: 'name',
      entryValue: 'ingredientId',
      label: 'Zutat',
      unavailableReason: null,
      addLabel: 'Hinzufügen',
      searchTestName: 'ingredient-to-add',
      addTestName: 'add-ingredient',
      chosenId: null,
    })
    await openTheListOf('[data-test="ingredient-to-add"]')

    await pressBack()

    expect({ open: isVisibleOnScreen(AN_OPEN_LIST), leftTheApp: theAppWasLeftByBack() }).toEqual({ open: false, leftTheApp: false })
  })
})
