import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useSessionStore } from '../../src/stores/session'

function withBrowserLanguage(language: string): void {
  vi.stubGlobal('navigator', { language, userAgent: 'test' })
}

describe('the language a phone starts in', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('starts in German when the browser asks for German', () => {
    withBrowserLanguage('de-DE')

    expect(useSessionStore().language).toBe('de')
  })

  it('starts in English when the browser asks for English', () => {
    withBrowserLanguage('en-GB')

    expect(useSessionStore().language).toBe('en')
  })

  it('falls back to German for any other browser language', () => {
    withBrowserLanguage('fr-FR')

    expect(useSessionStore().language).toBe('de')
  })

  it('prefers the language stored on the device over the browser', () => {
    withBrowserLanguage('de-DE')
    localStorage.setItem('language', 'en')

    expect(useSessionStore().language).toBe('en')
  })
})
