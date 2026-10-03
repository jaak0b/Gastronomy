import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AdminShell from '../../../src/admin/views/AdminShell.vue'
import { navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'
import { stubLaptop, answer, emptyAnswer } from '../../support/laptop'

function shellLaptop(adminStatus: number): void {
  stubLaptop()
    .answersEverythingElse(
      adminStatus === 200
        ? answer({ festivals: [], staffMembers: [], stations: [], categories: [], items: [] })
        : emptyAnswer(adminStatus),
    )
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
})
