import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { answer, stubLaptop } from './support/laptop'
import {
  aBrowserThatCountsTheTap,
  aBrowserWithoutUserActivation,
  aTabThatOpenedTheAppAt,
  pressBack,
  theAppWasLeftByBack,
  theNextMoveThroughTheHistory,
} from './support/browserHistory'
import { allOnScreen, clickOn, isOnScreen, textOnScreen, waitUntil } from './support/dom'
import { testPlugins } from './support/plugins'
import { aFestival, anAdminArea } from './support/wireViews'

const SOMMERFEST = aFestival({ festivalId: 'fest-sommer', name: 'Sommerfest' })

let admin: VueWrapper | null = null

function aLaptopWithTheSommerfest(): void {
  stubLaptop()
    .answersEverythingElse(answer({ stations: [] }))
    .answers('GET', '/api/language', answer({ language: 'de' }))
    .answers('GET', '/api/admin/festivals', answer({ festivals: [SOMMERFEST] }))
    .answers('GET', '/api/admin/festivals/fest-sommer/table-plan', answer({ areas: [anAdminArea({ areaId: 'area-halle' })] }))
}

async function theAdminOnTheFestivalList(): Promise<void> {
  const { startRouter } = await import('../src/shared/router/router')
  startRouter()
  const AdminShell = (await import('../src/admin/views/AdminShell.vue')).default
  admin = mount(AdminShell, { global: { plugins: testPlugins() }, attachTo: document.body })
  await waitUntil(() => expect(isOnScreen('[data-test="open"]')).toBe(true))
}

async function openTheTab(title: string): Promise<void> {
  const tab = allOnScreen('[data-test="admin-tab"]').find((candidate) => candidate.textContent?.trim() === title)
  if (tab === undefined) {
    throw new Error(`the admin has no tab named ${title}`)
  }
  tab.click()
  await waitUntil(() => expect(textOnScreen('[data-test="admin-tab"][data-selected="true"]').trim()).toBe(title))
}

function theSelectedTab(): string {
  return textOnScreen('[data-test="admin-tab"][data-selected="true"]').trim()
}

beforeEach(() => {
  vi.resetModules()
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
  aTabThatOpenedTheAppAt('/admin/festivals')
  aBrowserThatCountsTheTap()
  aLaptopWithTheSommerfest()
})

afterEach(() => {
  admin?.unmount()
  admin = null
  aBrowserWithoutUserActivation()
})

describe('back in the admin', () => {
  it('lands on the festival list after switching between tabs', async () => {
    await theAdminOnTheFestivalList()
    await openTheTab('Artikel')
    await openTheTab('Zutaten')
    await openTheTab('Kellner')

    await pressBack()

    expect({ tab: theSelectedTab(), list: isOnScreen('[data-test="open"]') }).toEqual({ tab: 'Feste', list: true })
  })

  it('reaches the doors from the festival list, the first screen', async () => {
    await theAdminOnTheFestivalList()
    await openTheTab('Artikel')
    await pressBack()

    await pressBack()

    expect(theAppWasLeftByBack()).toBe(true)
  })

  it('returns from the editor of an area to the page of its festival', async () => {
    await theAdminOnTheFestivalList()
    await clickOn('[data-test="open"]')
    await waitUntil(() => expect(isOnScreen('[data-test="open-plan"]')).toBe(true))
    await clickOn('[data-test="open-plan"]')
    await waitUntil(() => expect(isOnScreen('[data-test="table-plan-editor"]')).toBe(true))

    await pressBack()

    expect({ page: textOnScreen('[data-test="festival-name"]').trim(), editor: isOnScreen('[data-test="table-plan-editor"]') }).toEqual({
      page: 'Sommerfest',
      editor: false,
    })
  })

  it('goes on up to the festival list after the way back from the editor of an area', async () => {
    await theAdminOnTheFestivalList()
    await clickOn('[data-test="open"]')
    await waitUntil(() => expect(isOnScreen('[data-test="open-plan"]')).toBe(true))
    await clickOn('[data-test="open-plan"]')
    await waitUntil(() => expect(isOnScreen('[data-test="table-plan-editor"]')).toBe(true))
    const theWayUp = theNextMoveThroughTheHistory()
    await clickOn('[data-test="back-to-festival"]')
    await theWayUp

    await pressBack()

    expect({ tab: theSelectedTab(), list: isOnScreen('[data-test="open"]') }).toEqual({ tab: 'Feste', list: true })
  })
})
