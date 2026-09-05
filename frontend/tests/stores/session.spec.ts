import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { useSessionStore } from '../../src/stores/session'
import { clearDraft, saveDraft } from '../../src/core/draftCart'

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

describe('the order a phone was holding when it was set up again', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  it('is admitted while the order is still there', () => {
    saveDraft({
      tableName: 'Tisch 12',
      note: null,
      lines: [
        {
          catalogItemId: 'item-1',
          note: null,
          stationId: null,
          name: 'Bratwurst',
          unitPriceCents: 350,
        },
      ],
      clientOrderId: null,
    })

    expect(useSessionStore().heldDraftExists()).toBe(true)
  })

  it('is no longer claimed once the order has been sent and cleared', () => {
    saveDraft({
      tableName: 'Tisch 12',
      note: null,
      lines: [
        {
          catalogItemId: 'item-1',
          note: null,
          stationId: null,
          name: 'Bratwurst',
          unitPriceCents: 350,
        },
      ],
      clientOrderId: null,
    })
    const session = useSessionStore()
    expect(session.heldDraftExists()).toBe(true)

    clearDraft()

    expect(session.heldDraftExists()).toBe(false)
  })
})
