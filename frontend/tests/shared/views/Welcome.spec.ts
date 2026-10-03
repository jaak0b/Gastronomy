import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Welcome from '../../../src/shared/views/Welcome.vue'
import { testPlugins } from '../../support/plugins'
import { stubLaptop, answer } from '../../support/laptop'

function mountWelcome(locale: 'de' | 'en' = 'de') {
  return mount(Welcome, { global: { plugins: testPlugins(locale) } })
}

async function openTheOptions(welcome: ReturnType<typeof mountWelcome>): Promise<void> {
  await welcome.get('[data-test="language-switch"] .v-field').trigger('mousedown')
  await vi.waitFor(() => expect(document.querySelector('[data-test="option-en"]')).not.toBeNull())
}

describe('the screen a device lands on with no code', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('says the device is not set up rather than demanding a code', () => {
    const welcome = mountWelcome()

    expect(welcome.get('h1').text()).toBe('Dieses Gerät ist noch nicht eingerichtet.')
  })

  it('says the same thing in English', () => {
    const welcome = mountWelcome('en')

    expect(welcome.get('h1').text()).toBe('This device is not set up yet.')
  })
})

describe('the language switch before a phone is set up', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    document.body.innerHTML = ''
    stubLaptop().answersEverythingElse(answer({}))
  })

  it('is on the screen, because nothing has stored a language yet', () => {
    const welcome = mountWelcome()

    expect(welcome.find('[data-test="language-switch"]').exists()).toBe(true)
  })

  it('remembers the choice on the device itself', async () => {
    localStorage.setItem('language', 'de')
    const welcome = mountWelcome()
    await openTheOptions(welcome)
    ;(document.querySelector('[data-test="option-en"]') as HTMLElement).click()

    expect(localStorage.getItem('language')).toBe('en')
  })

  it('asks the laptop for nothing, because this phone has no device of its own yet', async () => {
    localStorage.setItem('language', 'de')
    const welcome = mountWelcome()
    await openTheOptions(welcome)
    ;(document.querySelector('[data-test="option-en"]') as HTMLElement).click()

    expect(fetch).not.toHaveBeenCalled()
  })
})
