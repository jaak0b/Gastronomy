import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AdminShell from '../../../src/admin/views/AdminShell.vue'
import { navigate } from '../../../src/shared/router/router'
import { testPlugins } from '../../support/plugins'

function stubTheLaptop(adminStatus: number) {
  const fetchStub = vi.fn(async (url: string) => {
    if (url.startsWith('/api/language')) {
      return new Response(JSON.stringify({ language: 'de' }), { status: 200 })
    }
    if (adminStatus !== 200) {
      return new Response(null, { status: adminStatus })
    }
    return new Response(
      JSON.stringify({ festivals: [], staffMembers: [], stations: [], categories: [], items: [] }),
      { status: 200 },
    )
  })
  vi.stubGlobal('fetch', fetchStub)
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
    stubTheLaptop(404)

    const shell = mountShellAt('/admin/festivals')

    await vi.waitFor(() => expect(shell.find('.not-on-laptop').exists()).toBe(true))
    expect(shell.find('.admin-tabs').exists()).toBe(false)
  })

  it('says the admin pages only open on the laptop when the staff screen is refused', async () => {
    stubTheLaptop(404)

    const shell = mountShellAt('/admin/staff')

    await vi.waitFor(() => expect(shell.find('.not-on-laptop').exists()).toBe(true))
    expect(shell.find('.admin-tabs').exists()).toBe(false)
  })

  it('shows the admin tabs when the laptop answers', async () => {
    stubTheLaptop(200)

    const shell = mountShellAt('/admin/staff')
    await flushPromises()

    expect(shell.find('.admin-tabs .is-selected').text()).toBe('Kellner')
    expect(shell.find('.not-on-laptop').exists()).toBe(false)
  })
})
