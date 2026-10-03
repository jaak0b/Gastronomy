import { afterEach, describe, expect, it, vi } from 'vitest'
import { mount } from '@vue/test-utils'
import LanguageSwitch from '../../../src/shared/components/LanguageSwitch.vue'
import { testPlugins } from '../../support/plugins'

function mountSwitch(language: 'de' | 'en', locale: 'de' | 'en' = 'de') {
  return mount(LanguageSwitch, {
    props: { language },
    global: { plugins: testPlugins(locale) },
    attachTo: document.body,
  })
}

async function openTheOptions(control: ReturnType<typeof mountSwitch>): Promise<void> {
  await control.get('[data-test="language-switch"] .v-field').trigger('mousedown')
  await vi.waitFor(() => expect(document.querySelector('[data-test="option-de"]')).not.toBeNull())
}

describe('LanguageSwitch', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('shows the language in force on the closed picker', () => {
    const control = mountSwitch('de')

    expect(control.get('[data-test="language-switch"]').text()).toBe('Deutsch')
  })

  it('shows the English name when English is in force', () => {
    const control = mountSwitch('en', 'en')

    expect(control.get('[data-test="language-switch"]').text()).toBe('English')
  })

  it('writes each option in its own language, so a reader finds their own', async () => {
    const control = mountSwitch('de')
    await openTheOptions(control)

    expect(document.querySelector('[data-test="option-de"]')?.textContent?.trim()).toBe('Deutsch')
    expect(document.querySelector('[data-test="option-en"]')?.textContent?.trim()).toBe('English')
  })

  it('writes the options the same way when the page is already in English', async () => {
    const control = mountSwitch('en', 'en')
    await openTheOptions(control)

    expect(document.querySelector('[data-test="option-de"]')?.textContent?.trim()).toBe('Deutsch')
    expect(document.querySelector('[data-test="option-en"]')?.textContent?.trim()).toBe('English')
  })

  it('asks for English when the reader taps English', async () => {
    const control = mountSwitch('de')
    await openTheOptions(control)

    ;(document.querySelector('[data-test="option-en"]') as HTMLElement).click()

    expect(control.emitted('select')).toEqual([['en']])
  })

  it('asks for German when the reader taps German', async () => {
    const control = mountSwitch('en', 'en')
    await openTheOptions(control)

    ;(document.querySelector('[data-test="option-de"]') as HTMLElement).click()

    expect(control.emitted('select')).toEqual([['de']])
  })
})
