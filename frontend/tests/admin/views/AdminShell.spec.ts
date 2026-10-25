import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AdminShell from '../../../src/admin/views/AdminShell.vue'
import { navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { stubLaptop, answer, emptyAnswer } from '../../support/laptop'
import { aFestival, anAdminArea } from '../../support/wireViews'

function shellLaptop(adminStatus: number): void {
  stubLaptop()
    .answersEverythingElse(
      adminStatus === 200
        ? answer({ festivals: [], staffMembers: [], stations: [], categories: [], items: [] })
        : emptyAnswer(adminStatus),
    )
    .answers('GET', '/api/admin/units', answer({ units: [] }))
    .answers('GET', '/api/language', answer({ language: 'de' }))
}

function mountShellAt(path: string) {
  navigate(path)
  return mount(AdminShell, { global: { plugins: testPlugins('de') } })
}

describe('the admin shell', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('says the admin pages only open on the laptop when the festivals screen is refused', async () => {
    shellLaptop(404)

    const shell = mountShellAt('/admin/festivals')

    await vi.waitFor(() => expect(shell.find('[data-test="not-on-laptop"]').exists()).toBe(true))
    expect(shell.find('[data-test="admin-tabs"]').exists()).toBe(false)
  })

  it('says the admin pages only open on the laptop when the staff screen is refused', async () => {
    shellLaptop(404)

    const shell = mountShellAt('/admin/staff')

    await vi.waitFor(() => expect(shell.find('[data-test="not-on-laptop"]').exists()).toBe(true))
    expect(shell.find('[data-test="admin-tabs"]').exists()).toBe(false)
  })

  it('shows the admin tabs when the laptop answers', async () => {
    shellLaptop(200)

    const shell = mountShellAt('/admin/staff')
    await flushPromises()

    expect(shell.find('[data-test="admin-tabs"] [data-test="admin-tab"][data-selected="true"]').text()).toBe('Kellner')
    expect(shell.find('[data-test="not-on-laptop"]').exists()).toBe(false)
  })

  it('opens the editor of one area at the address of that area', async () => {
    stubLaptop()
      .answersEverythingElse(answer({ stations: [] }))
      .answers('GET', '/api/language', answer({ language: 'de' }))
      .answers('GET', '/api/admin/festivals', answer({ festivals: [aFestival({ festivalId: 'fest-1' })] }))
      .answers('GET', '/api/admin/festivals/fest-1/table-plan', answer({ areas: [anAdminArea({ areaId: 'area-halle' })] }))

    const shell = mountShellAt('/admin/festivals/fest-1/areas/area-halle')
    await flushPromises()

    expect(shell.find('[data-test="table-plan-editor"]').exists()).toBe(true)
    expect(shell.find('[data-test="festival-name-field"]').exists()).toBe(false)
  })

  it('places the units tab between the ingredients and the waiters', async () => {
    shellLaptop(200)

    const shell = mountShellAt('/admin/ingredients')
    await flushPromises()

    expect(shell.findAll('[data-test="admin-tabs"] [data-test="admin-tab"]').map((tab) => tab.text())).toEqual([
      'Übersicht',
      'Feste',
      'Ausgabestellen',
      'Artikel',
      'Zutaten',
      'Einheiten',
      'Kellner',
    ])
    expect(shell.find('[data-test="admin-tabs"] [data-test="admin-tab"][data-selected="true"]').text()).toBe('Zutaten')
  })

  it('opens the units tab with its own heading', async () => {
    shellLaptop(200)

    const shell = mountShellAt('/admin/units')
    await flushPromises()

    expect(shell.find('[data-test="admin-tabs"] [data-test="admin-tab"][data-selected="true"]').text()).toBe('Einheiten')
    expect(shell.find('[data-test="admin-units"]').exists()).toBe(true)
  })
})
