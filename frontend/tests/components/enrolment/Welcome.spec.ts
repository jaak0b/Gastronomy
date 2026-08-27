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

describe('the screen a phone lands on with no code', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('says the phone is not set up rather than demanding a code', () => {
    const welcome = mountWelcome()

    expect(welcome.get('h1').text()).toBe('Dieses Telefon ist noch nicht eingerichtet.')
  })

  it('sends the reader to the person at the laptop and names what they tap there', () => {
    const welcome = mountWelcome()

    expect(welcome.get('.welcome-body').text()).toBe(
      'Bitten Sie die Person am Laptop, Sie als Bedienung anzulegen. Sie tippt dort auf "Neue Bedienung" und zeigt Ihnen den QR-Code, den Sie mit der Kamera scannen.',
    )
  })

  it('asks for no code before the reader says they have one', () => {
    const welcome = mountWelcome()

    expect(welcome.find('.code-field').exists()).toBe(false)
  })

  it('offers the code entry underneath for somebody who was given a code', () => {
    const welcome = mountWelcome()

    expect(welcome.get('.open-code-entry').text()).toBe('Sechsstelligen Code eingeben')
  })

  it('opens the code entry when the reader says they have a code', async () => {
    const welcome = mountWelcome()

    await welcome.get('.open-code-entry').trigger('click')

    expect(welcome.find('.code-field').exists()).toBe(true)
  })

  it('says the same thing in English', () => {
    const welcome = mountWelcome('en')

    expect(welcome.get('h1').text()).toBe('This phone is not set up yet.')
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

  it('remembers the choice on the device the same way the station page does', async () => {
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
