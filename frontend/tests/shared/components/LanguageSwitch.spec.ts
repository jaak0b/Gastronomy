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
  await control.get('.language-switch .v-field').trigger('mousedown')
  await vi.waitFor(() => expect(document.querySelector('.option-de')).not.toBeNull())
}

describe('LanguageSwitch', () => {
  afterEach(() => {
    document.body.innerHTML = ''
  })

  it('shows the language in force on the closed picker', () => {
    const control = mountSwitch('de')

    expect(control.get('.language-switch').text()).toBe('Deutsch')
  })

  it('shows the English name when English is in force', () => {
    const control = mountSwitch('en', 'en')

    expect(control.get('.language-switch').text()).toBe('English')
  })

  it('writes each option in its own language, so a reader finds their own', async () => {
    const control = mountSwitch('de')
    await openTheOptions(control)

    expect(document.querySelector('.option-de')?.textContent?.trim()).toBe('Deutsch')
    expect(document.querySelector('.option-en')?.textContent?.trim()).toBe('English')
  })

  it('writes the options the same way when the page is already in English', async () => {
    const control = mountSwitch('en', 'en')
    await openTheOptions(control)

    expect(document.querySelector('.option-de')?.textContent?.trim()).toBe('Deutsch')
    expect(document.querySelector('.option-en')?.textContent?.trim()).toBe('English')
  })

  it('asks for English when the reader taps English', async () => {
    const control = mountSwitch('de')
    await openTheOptions(control)

    ;(document.querySelector('.option-en') as HTMLElement).click()

    expect(control.emitted('select')).toEqual([['en']])
  })

  it('asks for German when the reader taps German', async () => {
    const control = mountSwitch('en', 'en')
    await openTheOptions(control)

    ;(document.querySelector('.option-de') as HTMLElement).click()

    expect(control.emitted('select')).toEqual([['de']])
  })

  it('is named for a screen reader even though no label is visible', () => {
    const control = mountSwitch('de')

    expect(control.get('input[aria-label]').attributes('aria-label')).toBe('Sprache')
  })
})
