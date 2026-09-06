import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createI18n } from 'vue-i18n'
import Welcome from '../../../src/views/Welcome.vue'
import de from '../../../src/locales/de.json'
import en from '../../../src/locales/en.json'

function mountWelcome(locale: 'de' | 'en' = 'de') {
  const i18n = createI18n({ legacy: false, locale, messages: { de, en } })
  return mount(Welcome, { global: { plugins: [i18n] } })
}

describe('the screen a device lands on with no code', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('says the device is not set up rather than demanding a code', () => {
    const welcome = mountWelcome()

    expect(welcome.get('h1').text()).toBe('Dieses Gerät ist noch nicht eingerichtet.')
  })

  it('sends the reader to the person at the laptop, whether it is a phone or a tablet', () => {
    const welcome = mountWelcome()

    expect(welcome.get('.welcome-body').text()).toBe(
      'Bitten Sie die Person am Laptop, Sie als Kellner anzulegen oder das Tablet einer Ausgabestelle einzurichten. Sie zeigt Ihnen einen QR-Code, den Sie mit der Kamera scannen.',
    )
  })

  it('asks for no code before the reader says they have one', () => {
    const welcome = mountWelcome()

    expect(welcome.find('.code-field').exists()).toBe(false)
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
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('is on the screen, because nothing has stored a language yet', () => {
    const welcome = mountWelcome()

    expect(welcome.get('.language-switch').exists()).toBe(true)
  })

  it('remembers the choice on the device itself', async () => {
    const welcome = mountWelcome()

    await welcome.get('.option-en').trigger('click')

    expect(localStorage.getItem('language')).toBe('en')
  })

  it('asks the laptop for nothing, because this phone has no device of its own yet', async () => {
    const welcome = mountWelcome()

    await welcome.get('.option-en').trigger('click')

    expect(fetch).not.toHaveBeenCalled()
  })
})
