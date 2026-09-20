import { beforeEach, describe, expect, it } from 'vitest'
import {
  appLanguageOf,
  browserLanguage,
  initialLanguage,
  LANGUAGE_STORAGE_KEY,
} from '../../../src/shared/core/deviceLanguage'

function withTheBrowserLanguage(language: string): void {
  Object.defineProperty(window.navigator, 'language', { value: language, configurable: true })
}

describe('the language a device starts in', () => {
  beforeEach(() => {
    localStorage.clear()
  })

  it('reads a browser in German, Austrian or Swiss German as German', () => {
    withTheBrowserLanguage('de')
    expect(browserLanguage()).toBe('de')
    withTheBrowserLanguage('de-AT')
    expect(browserLanguage()).toBe('de')
    withTheBrowserLanguage('de-CH')
    expect(browserLanguage()).toBe('de')
  })

  it('reads every other browser as English', () => {
    withTheBrowserLanguage('en-US')
    expect(browserLanguage()).toBe('en')
    withTheBrowserLanguage('fr-FR')
    expect(browserLanguage()).toBe('en')
    withTheBrowserLanguage('it-IT')
    expect(browserLanguage()).toBe('en')
  })

  it('keeps the language the device stored earlier', () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'de')
    withTheBrowserLanguage('en-US')

    expect(initialLanguage()).toBe('de')
  })

  it('falls back to the browser language when the device stored nothing', () => {
    withTheBrowserLanguage('fr-FR')

    expect(initialLanguage()).toBe('en')
  })

  it('falls back to the browser language when the stored value is not one of the two', () => {
    withTheBrowserLanguage('fr-FR')
    localStorage.setItem(LANGUAGE_STORAGE_KEY, '{"language":"de"}')

    expect(initialLanguage()).toBe('en')
  })
})

describe('reading a locale as one of the application languages', () => {
  it('takes the two languages the application is written in', () => {
    expect(appLanguageOf('de')).toBe('de')
    expect(appLanguageOf('en')).toBe('en')
  })

  it('refuses anything else instead of picking a language for the reader', () => {
    expect(() => appLanguageOf('fr')).toThrowError(/fr/)
  })
})
