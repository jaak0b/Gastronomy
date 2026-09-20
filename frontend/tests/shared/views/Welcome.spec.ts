import { beforeEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import Welcome from '../../../src/shared/views/Welcome.vue'
import { testPlugins } from '../../support/plugins'

function mountWelcome(locale: 'de' | 'en' = 'de') {
  return mount(Welcome, { global: { plugins: testPlugins(locale) } })
}

async function openTheOptions(welcome: ReturnType<typeof mountWelcome>): Promise<void> {
  await welcome.get('.language-switch .v-field').trigger('mousedown')
  await vi.waitFor(() => expect(document.querySelector('.option-en')).not.toBeNull())
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
    document.body.innerHTML = ''
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 200 })))
  })

  it('is on the screen, because nothing has stored a language yet', () => {
    const welcome = mountWelcome()

    expect(welcome.get('.language-switch').exists()).toBe(true)
  })

  it('remembers the choice on the device itself', async () => {
    localStorage.setItem('language', 'de')
    const welcome = mountWelcome()
    await openTheOptions(welcome)
    ;(document.querySelector('.option-en') as HTMLElement).click()

    expect(localStorage.getItem('language')).toBe('en')
  })

  it('asks the laptop for nothing, because this phone has no device of its own yet', async () => {
    localStorage.setItem('language', 'de')
    const welcome = mountWelcome()
    await openTheOptions(welcome)
    ;(document.querySelector('.option-en') as HTMLElement).click()

    expect(fetch).not.toHaveBeenCalled()
  })
})
