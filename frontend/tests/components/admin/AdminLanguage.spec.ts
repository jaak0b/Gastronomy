import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import AdminShell from '../../../src/views/admin/AdminShell.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

function mountShell() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  const shell = mount(AdminShell, { global: { plugins: [i18n] } })
  return { shell, i18n }
}

describe('the language switch on the admin', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    window.history.replaceState({}, '', '/admin')
    vi.stubGlobal(
      'fetch',
      vi.fn(
        async () =>
          new Response(JSON.stringify({ locations: [], items: [], printers: [] }), { status: 200 }),
      ),
    )
  })

  it('sits in the nav row, where the admin can find it', () => {
    const { shell } = mountShell()

    expect(shell.get('.admin-nav .language-switch').exists()).toBe(true)
  })

  it('offers both languages by their own names', () => {
    const { shell } = mountShell()

    expect(shell.get('.admin-nav .option-de').text()).toBe('Deutsch')
    expect(shell.get('.admin-nav .option-en').text()).toBe('English')
  })

  it('switches the whole admin the moment English is tapped', async () => {
    const { shell, i18n } = mountShell()

    await shell.get('.admin-nav .option-en').trigger('click')

    expect(i18n.global.locale.value).toBe('en')
  })

  it('renames the tabs into English', async () => {
    const { shell } = mountShell()

    await shell.get('.admin-nav .option-en').trigger('click')

    expect(shell.get('.admin-tabs button').text()).toBe('Overview')
  })

  it('remembers the choice on the laptop', async () => {
    const { shell } = mountShell()

    await shell.get('.admin-nav .option-en').trigger('click')

    expect(localStorage.getItem('language')).toBe('en')
  })
})
