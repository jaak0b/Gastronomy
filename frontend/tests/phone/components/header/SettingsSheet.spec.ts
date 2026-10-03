import { beforeEach, describe, expect, it, vi } from 'vitest'
import { defineComponent } from 'vue'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import AppHeader from '../../../../src/phone/components/header/AppHeader.vue'
import { useLocaleBinding } from '../../../../src/shared/composables/useLocaleBinding'
import { useSessionStore } from '../../../../src/shared/stores/session'
import { testPlugins } from '../../../support/plugins'
import { stubLaptop, answer } from '../../../support/laptop'

const AppBarStub = {
  template: '<div class="app-header"><slot /></div>',
}

const HeaderFollowingTheLanguage = defineComponent({
  components: { AppHeader },
  setup() {
    const session = useSessionStore()
    useLocaleBinding(() => session.language)
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
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('writes the sheet in the language the reader chooses', async () => {
    const header = mountHeader()

    await header.get('[data-test="settings"]').trigger('click')
    await vi.waitFor(() => expect(document.querySelector('[data-test="settings-sheet"]')).not.toBeNull())

    expect(document.querySelector('[data-test="settings-sheet"] [data-test="settings-title"]')?.textContent?.trim()).toBe(
      'Einstellungen',
    )

    const field = document.querySelector('[data-test="settings-sheet"] [data-test="language-switch"] .v-field')
    field?.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }))
    await vi.waitFor(() => expect(document.querySelector('[data-test="option-en"]')).not.toBeNull())
    ;(document.querySelector('[data-test="option-en"]') as HTMLElement).click()
    await flushPromises()

    expect(document.querySelector('[data-test="settings-sheet"] [data-test="settings-title"]')?.textContent?.trim()).toBe(
      'Settings',
    )
  })
})
