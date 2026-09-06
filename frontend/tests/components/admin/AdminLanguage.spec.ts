import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import AdminShell from '../../../src/views/admin/AdminShell.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

function stubFetchWithLanguage(language: string) {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      const payload = url.startsWith('/api/language')
        ? { language }
        : { stations: [], items: [] }
      return new Response(JSON.stringify(payload), { status: 200 })
    }),
  )
}

function mountShell() {
  const i18n = createI18n({ legacy: false, locale: 'de', messages: { de, en } })
  const shell = mount(AdminShell, { global: { plugins: [i18n] } })
  return { shell, i18n }
}

describe('the language the admin screens are written in', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    window.history.replaceState({}, '', '/admin')
  })

  it('offers no switch of its own, because the laptop decides', async () => {
    stubFetchWithLanguage('de')

    const { shell } = mountShell()
    await vi.waitFor(() => expect(shell.find('.admin-tabs').exists()).toBe(true))

    expect(shell.find('.language-switch').exists()).toBe(false)
  })

  it('follows the language chosen on the laptop', async () => {
    stubFetchWithLanguage('en')

    const { i18n } = mountShell()

    await vi.waitFor(() => expect(i18n.global.locale.value).toBe('en'))
  })

  it('writes the tabs in that language', async () => {
    stubFetchWithLanguage('en')

    const { shell } = mountShell()

    await vi.waitFor(() => expect(shell.get('.admin-tabs .v-tab').text()).toBe('Overview'))
  })

  it('stays in German when the laptop is set to German', async () => {
    stubFetchWithLanguage('de')

    const { shell } = mountShell()

    await vi.waitFor(() => expect(shell.get('.admin-tabs .v-tab').text()).toBe('Übersicht'))
  })
})
