import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { testPlugins } from '../../support/plugins'
import { clickOn, isOnScreen, textOnScreen } from '../../support/dom'
import { formDialogHolding } from '../../support/formDialogs'
import { answer, emptyAnswer, inTurn, noConnection, stubLaptop, type StubbedLaptop } from '../../support/laptop'
import { aFestival, anAdminItem } from '../../support/wireViews'
import {
  forgetHubEvents,
  holdBackGroupsJoined,
  theConnectionCloses,
  theLaptopSendsGroupsJoined,
  theWifiComesBack,
} from '../../support/hubConnection'

vi.mock('@microsoft/signalr', async () => (await import('../../support/hubConnection')).signalrModuleFake())

const { default: AdminShell } = await import('../../../src/admin/views/AdminShell.vue')
const { navigate } = await import('../../../src/shared/router/router')
const { useConnectionStore } = await import('../../../src/shared/stores/connection')

const LOAD_FAILED = 'Laden Sie die Seite neu. Die Daten konnten nicht geladen werden.'
const LOAD_FAILED_EN = 'Reload the page. The data could not be loaded.'

function adminLaptop(): StubbedLaptop {
  return stubLaptop()
    .answersEverythingElse(emptyAnswer(204))
    .answers('GET', '/api/language', answer({ language: 'de' }))
    .answers('GET', '/api/admin/festivals', answer({ festivals: [aFestival({ festivalId: 'fest-1' })] }))
    .answers('GET', /^\/api\/admin\/stations/, answer({ stations: [] }))
    .answers('GET', '/api/admin/categories', answer({ categories: [] }))
    .answers('GET', /^\/api\/admin\/items/, answer({ items: [] }))
    .answers('GET', '/api/admin/ingredients', answer({ ingredients: [] }))
    .answers('GET', '/api/admin/units', answer({ units: [] }))
    .answers('GET', '/api/admin/staff-members', answer({ staffMembers: [] }))
    .answers('GET', '/api/admin/festivals/fest-1/ingredients', answer({ ingredients: [] }))
    .answers('GET', '/api/admin/festivals/fest-1/table-plan', answer({ areas: [] }))
}

function adminLaptopWithOneArticle(): StubbedLaptop {
  return adminLaptop()
    .answers(
      'GET',
      '/api/admin/categories',
      answer({
        categories: [{ categoryId: 'category-speisen', name: 'Speisen', colourHex: '#FFEB3B', sortOrder: 1, isActive: true }],
      }),
    )
    .answers('GET', /^\/api\/admin\/items/, answer({ items: [anAdminItem()] }))
}

async function openTheAdminAt(path: string) {
  navigate(path)
  const shell = mount(AdminShell, { global: { plugins: testPlugins('de') } })
  await useConnectionStore().connect({})
  await flushPromises()
  return shell
}

describe('the load failure bar of the admin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is hidden while every list on the screen loaded', async () => {
    adminLaptop()

    const shell = await openTheAdminAt('/admin/items')

    expect(shell.find('[data-test="load-failure-bar"]').exists()).toBe(false)
  })

  it('says the data could not be loaded when the units of the Artikel tab fail', async () => {
    adminLaptop().answers('GET', '/api/admin/units', noConnection())

    const shell = await openTheAdminAt('/admin/items')

    expect(shell.get('[data-test="load-failure-bar"]').text()).toBe(LOAD_FAILED)
  })

  it('says the same on the Kellner tab when the waiters fail', async () => {
    adminLaptop().answers('GET', '/api/admin/staff-members', noConnection())

    const shell = await openTheAdminAt('/admin/staff')

    expect(shell.get('[data-test="load-failure-bar"]').text()).toBe(LOAD_FAILED)
  })

  it('says the same on Übersicht when the items of the running festival fail', async () => {
    adminLaptop().answers('GET', '/api/admin/items?festivalId=fest-1', noConnection())

    const shell = await openTheAdminAt('/admin/overview')

    expect(shell.get('[data-test="load-failure-bar"]').text()).toBe(LOAD_FAILED)
  })

  it('sits directly below the tabs', async () => {
    adminLaptop().answers('GET', '/api/admin/units', noConnection())

    const shell = await openTheAdminAt('/admin/items')

    expect(shell.get('[data-test="admin-nav"]').element.nextElementSibling).toBe(
      shell.get('[data-test="load-failure-bar"]').element,
    )
  })

  it('says it in English on an English laptop', async () => {
    adminLaptop()
      .answers('GET', '/api/language', answer({ language: 'en' }))
      .answers('GET', '/api/admin/units', noConnection())

    const shell = await openTheAdminAt('/admin/items')

    expect(shell.get('[data-test="load-failure-bar"]').text()).toBe(LOAD_FAILED_EN)
  })
})

describe('the load failure bar inside the recipe of an article', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
    localStorage.clear()
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says the data could not be loaded inside the recipe when the ingredients fail', async () => {
    adminLaptopWithOneArticle().answers('GET', '/api/admin/ingredients', noConnection())
    const shell = await openTheAdminAt('/admin/items')

    await shell.get('[data-test="edit-ingredients"]').trigger('click')

    await vi.waitFor(() =>
      expect(document.querySelector('[data-test="form-dialog"] [data-test="load-failure-bar"]')?.textContent?.trim()).toBe(
        LOAD_FAILED,
      ),
    )
  })

  it('says it inside the form of a new ingredient when the units fail', async () => {
    adminLaptopWithOneArticle().answers('GET', '/api/admin/units', noConnection())
    const shell = await openTheAdminAt('/admin/items')
    await shell.get('[data-test="edit-ingredients"]').trigger('click')
    await vi.waitFor(() => expect(isOnScreen('[data-test="new-ingredient"]')).toBe(true))

    await clickOn('[data-test="new-ingredient"]')

    await vi.waitFor(() =>
      expect(
        textOnScreen('[data-test="load-failure-bar"]', formDialogHolding('[data-test="ingredient-name-field"]')).trim(),
      ).toBe(LOAD_FAILED),
    )
  })
})

describe('the load failure bar on the page of one festival', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('says it once when nothing of the festival can be loaded', async () => {
    adminLaptop()
      .answers('GET', '/api/admin/stations?festivalId=fest-1', noConnection())
      .answers('GET', '/api/admin/categories', noConnection())
      .answers('GET', '/api/admin/items?festivalId=fest-1', noConnection())
      .answers('GET', '/api/admin/festivals/fest-1/ingredients', noConnection())
      .answers('GET', '/api/admin/festivals/fest-1/table-plan', noConnection())

    const shell = await openTheAdminAt('/admin/festivals/fest-1')

    expect(shell.text().split(LOAD_FAILED)).toHaveLength(2)
  })

  it('says it when only the stations fail', async () => {
    adminLaptop().answers('GET', '/api/admin/stations?festivalId=fest-1', noConnection())

    const shell = await openTheAdminAt('/admin/festivals/fest-1')

    expect(shell.get('[data-test="load-failure-bar"]').text()).toBe(LOAD_FAILED)
  })
})

describe('the load failure bar while the laptop restarts', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    forgetHubEvents()
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('appears when the connection to the laptop is lost', async () => {
    adminLaptop()
    const shell = await openTheAdminAt('/admin/items')

    theConnectionCloses()
    await flushPromises()

    expect(shell.get('[data-test="load-failure-bar"]').text()).toBe(LOAD_FAILED)
  })

  it('goes away once the connection is back and the laptop has put the admin into its groups', async () => {
    adminLaptop()
    const shell = await openTheAdminAt('/admin/items')
    theConnectionCloses()
    await flushPromises()

    theWifiComesBack()
    theLaptopSendsGroupsJoined()
    await flushPromises()

    expect(shell.find('[data-test="load-failure-bar"]').exists()).toBe(false)
  })

  it('goes away once the list that failed loads after the reconnect', async () => {
    holdBackGroupsJoined()
    adminLaptop().answers('GET', '/api/admin/units', inTurn(noConnection(), answer({ units: [] })))
    const shell = await openTheAdminAt('/admin/items')
    await vi.waitFor(() => expect(shell.find('[data-test="load-failure-bar"]').exists()).toBe(true))

    theLaptopSendsGroupsJoined()
    await flushPromises()

    expect(shell.find('[data-test="load-failure-bar"]').exists()).toBe(false)
  })
})
