import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createI18n } from 'vue-i18n'
import LanguageSwitch from '../../src/components/LanguageSwitch.vue'
import de from '../../src/locales/de.json'
import en from '../../src/locales/en.json'

function mountSwitch(language: 'de' | 'en', locale: 'de' | 'en' = 'de') {
  const i18n = createI18n({ legacy: false, locale, messages: { de, en } })
  return mount(LanguageSwitch, {
    props: { language, labelKey: 'settings.language' },
    global: { plugins: [i18n] },
  })
}

describe('LanguageSwitch', () => {
  it('writes each option in its own language, so a reader finds their own', () => {
    const control = mountSwitch('de')

    expect(control.get('.option-de').text()).toBe('Deutsch')
    expect(control.get('.option-en').text()).toBe('English')
  })

  it('writes the options the same way when the page is already in English', () => {
    const control = mountSwitch('en', 'en')

    expect(control.get('.option-de').text()).toBe('Deutsch')
    expect(control.get('.option-en').text()).toBe('English')
  })

  it('shows which language is in force', () => {
    const control = mountSwitch('de')

    expect(control.get('.option-de').attributes('aria-pressed')).toBe('true')
    expect(control.get('.option-en').attributes('aria-pressed')).toBe('false')
  })

  it('carries the label its screen gives it', () => {
    const control = mountSwitch('de')

    expect(control.get('.label').text()).toBe('Sprache')
  })

  it('asks for English when the reader taps English', async () => {
    const control = mountSwitch('de')

    await control.get('.option-en').trigger('click')

    expect(control.emitted('select')).toEqual([['en']])
  })

  it('asks for German when the reader taps German', async () => {
    const control = mountSwitch('en')

    await control.get('.option-de').trigger('click')

    expect(control.emitted('select')).toEqual([['de']])
  })
})
