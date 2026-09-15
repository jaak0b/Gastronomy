import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppHeader from '../../../src/components/header/AppHeader.vue'
import { bindLocaleToSession } from '../../../src/localeBinding'
import { testPlugins } from '../../support/plugins'

const AppBarStub = {
  template: '<div class="app-header"><slot /></div>',
}

const HeaderFollowingTheLanguage = defineComponent({
  components: { AppHeader },
  setup() {
    bindLocaleToSession()
  },
  template: '<AppHeader />',
})

function mountHeader() {
  localStorage.setItem('language', 'de')
  return mount(HeaderFollowingTheLanguage, {
    global: {
      plugins: testPlugins(),
      stubs: { VAppBar: AppBarStub },
    },
    attachTo: document.body,
  })
}

describe('the language picker in the settings sheet', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('writes the sheet in the language the reader chooses', async () => {
    const header = mountHeader()

    await header.get('.settings').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('.settings-sheet')).not.toBeNull())

    expect(document.querySelector('.settings-sheet .v-card-title')?.textContent?.trim()).toBe(
      'Einstellungen',
    )

    const field = document.querySelector('.settings-sheet .language-switch .v-field')
    field?.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await vi.waitFor(() => expect(document.querySelector('.option-en')).not.toBeNull())
    ;(document.querySelector('.option-en') as HTMLElement).click()
    await flushPromises()

    expect(document.querySelector('.settings-sheet .v-card-title')?.textContent?.trim()).toBe(
      'Settings',
    )
  })
})
