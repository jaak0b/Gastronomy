import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { h } from 'vue'
import { VDefaultsProvider } from 'vuetify/components'
import { stubLaptop, answer, type StubbedLaptop } from './support/laptop'
import { onScreen, waitUntil } from './support/dom'

vi.mock('@microsoft/signalr', async () => (await import('./support/hubConnection')).signalrModuleFake())

const { navigate, currentRoute } = await import('../src/shared/router/router')
const { TOKEN_STORAGE_KEY } = await import('../src/shared/stores/session')
const { PHONE_DEFAULTS } = await import('../src/phone/styles/phoneSizes')
const TableActionSheet = (await import('../src/phone/components/tablePlan/TableActionSheet.vue')).default
const App = (await import('../src/App.vue')).default
const { testPlugins } = await import('./support/plugins')
const { stationLaptop, mountPage, cardInTheQueue, itemOnCard } = await import('./station/views/stationPageFixture')

enableAutoUnmount(afterEach)

function phoneLaptop(): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(
      answer({
        deviceId: 'device-1',
        staffMember: { id: 'staff-1', name: 'Anna' },
        station: null,
        language: 'de',
      }),
    )
    .answers('GET', /^\/api\/table-plan/, answer({ areas: [] }))
    .answers('GET', /^\/api\/estimates/, answer([]))
    .answers('GET', /^\/api\/catalog/, answer({ festival: null, categories: [], items: [], stations: [] }))
}

async function mountThePhone() {
  phoneLaptop()
  localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.token-here')
  navigate('/')
  const app = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
  await flushPromises()
  return app
}

function overlayContentAround(selector: string): HTMLElement {
  const content = onScreen(selector).closest('.v-overlay__content')
  if (!(content instanceof HTMLElement)) {
    throw new Error(`${selector} is not inside an overlay`)
  }
  return content
}

describe('the sizes of the waiter phone', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    sessionStorage.setItem('theDoorAnchor', 'yes')
    document.body.innerHTML = ''
  })

  it('draws the phone screens inside the phone sizes', async () => {
    const app = await mountThePhone()

    expect(app.get('[data-test="app"]').classes()).toContain('phone-sizes')
  })

  it('draws a button of the phone large', async () => {
    const app = await mountThePhone()

    expect(app.get('[data-test="app-header"] [data-test="settings"]').classes()).toContain('v-btn--size-large')
  })

  it('writes the text of an empty table plan in the body text class', async () => {
    const app = await mountThePhone()

    expect(app.get('[data-test="no-festival"]').classes()).toContain('app-text')
  })

  it('gives a dialog opened on the phone the phone sizes and its select the comfortable density', async () => {
    const app = await mountThePhone()

    await app.get('[data-test="app-header"] [data-test="settings"]').trigger('click')
    await waitUntil(() => expect(onScreen('[data-test="settings-sheet"]')).toBeTruthy())

    expect(overlayContentAround('[data-test="settings-sheet"]').classList).toContain('phone-sizes')
    expect(onScreen('[data-test="settings-sheet"] [data-test="language-switch"]').classList).toContain(
      'v-input--density-comfortable',
    )
  })

  it('gives a bottom sheet on the phone the phone sizes and its wide buttons the extra large size', async () => {
    mount(() => h(VDefaultsProvider, { defaults: PHONE_DEFAULTS }, () => h(TableActionSheet, { tableName: '3' })), {
      global: { plugins: testPlugins() },
      attachTo: document.body,
    })
    await waitUntil(() => expect(onScreen('[data-test="table-actions"]')).toBeTruthy())

    expect(overlayContentAround('[data-test="table-actions"]').classList).toContain('phone-sizes')
    expect(onScreen('[data-test="take-order"]').classList).toContain('v-btn--size-x-large')
    expect(onScreen('[data-test="take-order"]').classList).toContain('v-btn--block')
  })
})

describe('the sizes of the station tablet', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('draws the station page inside the station sizes', async () => {
    stationLaptop()
    const page = await mountPage()

    expect(page.get('[data-test="station-page"]').classes()).toContain('station-sizes')
  })

  it('draws a button of the station large without a size of its own', async () => {
    stationLaptop()
    const page = await mountPage()

    expect(page.get('[data-test="show-done"]').classes()).toContain('v-btn--size-large')
  })

  it('draws the language choice of the station in the comfortable density', async () => {
    stationLaptop()
    const page = await mountPage()

    expect(page.get('[data-test="language-switch"]').classes()).toContain('v-input--density-comfortable')
  })

  it('writes the table name on an order card in the section title class', async () => {
    stationLaptop()
    const page = await mountPage()

    expect(cardInTheQueue(page, 'station-order-1').get('[data-test="table-name"]').classes()).toContain(
      'app-section-title',
    )
  })

  it('gives the dialog that confirms done items the station sizes', async () => {
    stationLaptop()
    const page = await mountPage()
    const card = cardInTheQueue(page, 'station-order-1')

    await itemOnCard(card, 'a').trigger('click')
    await card.get('[data-test="fulfill"]').trigger('click')
    await waitUntil(() => expect(onScreen('[data-test="station-done-dialog"] [data-test="card"]')).toBeTruthy())

    expect(overlayContentAround('[data-test="station-done-dialog"] [data-test="card"]').classList).toContain(
      'station-sizes',
    )
  })
})

describe('the admin beside the phone sizes', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    sessionStorage.setItem('theDoorAnchor', 'yes')
    document.body.innerHTML = ''
    stubLaptop().answersEverythingElse(answer({ festivals: [], stations: [], items: [], staffMembers: [] }))
    currentRoute.value = { name: 'admin', section: 'festivals', festivalId: null }
    window.history.replaceState({}, '', '/admin/festivals')
  })

  it('keeps the admin out of the phone sizes and draws its buttons at the admin size', async () => {
    const app = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
    await vi.waitFor(() => expect(app.find('[data-test="new-festival"]').exists()).toBe(true))

    expect(app.get('[data-test="app"]').classes()).not.toContain('phone-sizes')
    expect(app.get('[data-test="admin-shell"]').classes()).toContain('admin-sizes')
    expect(app.get('[data-test="new-festival"]').classes()).toContain('v-btn--size-default')
  })
})
