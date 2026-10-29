import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AdminShell from '../../../src/admin/views/AdminShell.vue'
import AppHeader from '../../../src/phone/components/header/AppHeader.vue'
import { navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { stubLaptop, answer } from '../../support/laptop'
import { onScreen, waitUntil } from '../../support/dom'
import { aFestival } from '../../support/wireViews'
import { layOutTheScreen } from '../../support/konvaDrawing'
import { editorLaptop, HALL, openTheToolbarMenu, WINDOW_AT_ONE_AND_A_HALF } from '../components/tablePlan/tablePlanEditorFixture'

enableAutoUnmount(afterEach)

afterEach(() => {
  vi.restoreAllMocks()
})

const AppBarStub = {
  template: '<div class="app-header"><slot /></div>',
}

async function mountTheAdminAt(path: string) {
  stubLaptop()
    .answersEverythingElse(answer({ festivals: [], staffMembers: [], stations: [], categories: [], items: [] }))
    .answers('GET', '/api/admin/festivals', answer({ festivals: [aFestival({ festivalId: 'fest-1', isRunning: false })] }))
    .answers('GET', '/api/admin/units', answer({ units: [] }))
    .answers('GET', '/api/language', answer({ language: 'de' }))
  navigate(path)
  const shell = mount(AdminShell, { global: { plugins: testPlugins('de') }, attachTo: document.body })
  await flushPromises()
  return shell
}

function mountThePhoneHeader() {
  stubLaptop().answersEverythingElse(answer({}))
  navigate('/')
  return mount(AppHeader, {
    global: { plugins: testPlugins('de'), stubs: { VAppBar: AppBarStub } },
    attachTo: document.body,
  })
}

async function mountTheTablePlanEditor() {
  editorLaptop()
  layOutTheScreen(WINDOW_AT_ONE_AND_A_HALF)
  navigate(`/admin/festivals/fest-1/areas/${HALL.areaId}`)
  const shell = mount(AdminShell, { global: { plugins: testPlugins('de') }, attachTo: document.body })
  await waitUntil(() => expect(shell.find('[data-test="table-plan-toolbar"]').exists()).toBe(true))
  await flushPromises()
  return shell
}

function overlayContentAround(selector: string): HTMLElement {
  const element = onScreen(selector)
  const content = element.closest('.v-overlay__content') ?? element.querySelector('.v-overlay__content')
  if (!(content instanceof HTMLElement)) {
    throw new Error(`${selector} is not inside an overlay`)
  }
  return content
}

describe('the sizes of the controls on the laptop and on the phone', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
  })

  it('draws the buttons of the admin smaller than the phone default', async () => {
    const shell = await mountTheAdminAt('/admin/festivals')

    const button = shell.get('[data-test="new-festival"]')

    expect(button.classes()).toEqual(expect.arrayContaining(['v-btn--size-default', 'v-btn--density-comfortable']))
  })

  it('draws the shared icon action of the admin in the compact size', async () => {
    const shell = await mountTheAdminAt('/admin/festivals')

    const hideAction = shell.get('[data-test="festival-row"][data-test-id="fest-1"] [data-test="hide"]')

    expect(hideAction.classes()).toContain('v-btn--density-comfortable')
  })

  it('keeps the same shared icon action on the phone at its full size', () => {
    const header = mountThePhoneHeader()

    expect(header.get('[data-test="settings"]').classes()).toContain('v-btn--density-default')
  })

  it('gives a dialog opened in the admin the admin sizes', async () => {
    const shell = await mountTheAdminAt('/admin/festivals')

    await shell.get('[data-test="new-festival"]').trigger('click')
    await waitUntil(() => expect(onScreen('[data-test="form-dialog"]')).toBeTruthy())

    expect(overlayContentAround('[data-test="form-dialog"]').classList).toContain('admin-sizes')
  })

  it('draws the fields inside an admin dialog in the compact size', async () => {
    const shell = await mountTheAdminAt('/admin/items')

    await shell.get('[data-test="new-item"]').trigger('click')
    await waitUntil(() => expect(onScreen('[data-test="category-field"]')).toBeTruthy())

    expect(onScreen('[data-test="category-field"]').classList).toContain('v-input--density-compact')
  })

  it('gives the menu of a select in the admin the admin sizes', async () => {
    const shell = await mountTheAdminAt('/admin/items')
    await shell.get('[data-test="new-item"]').trigger('click')
    await waitUntil(() => expect(onScreen('[data-test="category-field"]')).toBeTruthy())

    onScreen('[data-test="category-field"] .v-field').dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await waitUntil(() => expect(document.querySelector('.v-select__content')).not.toBeNull())

    expect(document.querySelector('.v-select__content')?.classList).toContain('admin-sizes')
  })

  it('gives a tooltip shown in the admin the admin sizes', async () => {
    const shell = await mountTheAdminAt('/admin/festivals')

    await shell.get('[data-test="festival-row"][data-test-id="fest-1"] [data-test="hide"]').trigger('mouseenter')
    await waitUntil(() => expect(onScreen('[data-test="icon-action-label"]')).toBeTruthy())

    expect(overlayContentAround('[data-test="icon-action-label"]').classList).toContain('admin-sizes')
  })

  it('gives a plain menu opened in the admin the admin sizes', async () => {
    await mountTheTablePlanEditor()

    await openTheToolbarMenu('add-station', 'station-to-place')

    expect(overlayContentAround('[data-test="stations-to-place"]').classList).toContain('admin-sizes')
  })

  it('leaves a dialog opened on the phone at the phone sizes', async () => {
    const header = mountThePhoneHeader()

    await header.get('[data-test="settings"]').trigger('click')
    await waitUntil(() => expect(onScreen('[data-test="settings-sheet"]')).toBeTruthy())

    expect(overlayContentAround('[data-test="settings-sheet"]').classList).not.toContain('admin-sizes')
  })
})
