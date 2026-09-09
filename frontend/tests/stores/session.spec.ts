import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../src/stores/session'
import {
  clearDraft,
  restoreDraft,
  saveDraft,
  saveSendProgress,
} from '../../src/core/draftCart'
import { useOrderStore } from '../../src/stores/order'
import { request } from '../../src/api/client'

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

describe('setting a phone up for a waiter who is not on the list yet', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('sends the name the waiter typed along with the code', async () => {
    const bodies: unknown[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, options?: RequestInit) => {
        bodies.push(JSON.parse(String(options?.body ?? 'null')))
        return new Response(
          JSON.stringify({
            deviceToken: 'token-1',
            deviceKind: 'staffMember',
            staffMember: { id: 'staff-1', name: 'Bernd' },
            language: 'de',
          }),
          { status: 200 },
        )
      }),
    )

    await useSessionStore().redeem({ code: 'abc123', name: 'Bernd' })

    expect(bodies).toEqual([
      { code: 'abc123', name: 'Bernd', userAgent: navigator.userAgent },
    ])
  })
})

describe('a phone the laptop does not know any more', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function aLaptopThatRefusesTheToken(): void {
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 401 })))
  }

  async function anOrderRefusedBecauseTheTokenIsUnknown() {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-the-laptop-forgot')
    saveDraft({
      tableName: 'Tisch 12',
      note: null,
      lines: [{ catalogItemId: 'item-1', note: null, stationId: null, name: 'Bratwurst' }],
      clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
      deliveryModes: {},
    })
    const session = useSessionStore()
    session.watchForBeingSignedOut()
    aLaptopThatRefusesTheToken()

    await request('/api/orders', { method: 'POST', body: {}, token: session.deviceToken })
    return session
  }

  it('is signed out by the send itself, not only by the check the app makes when it starts', async () => {
    const session = await anOrderRefusedBecauseTheTokenIsUnknown()

    expect(session.deviceToken).toBeNull()
  })

  it('keeps the order the waiter had started, so it is still there after the phone is set up again', async () => {
    await anOrderRefusedBecauseTheTokenIsUnknown()

    expect(useSessionStore().heldDraftExists()).toBe(true)
  })

  it('keeps the identity of that order, so sending it again cannot place it twice', async () => {
    await anOrderRefusedBecauseTheTokenIsUnknown()

    expect(restoreDraft().draft.clientOrderId).toBe('c0ffee00-1111-4111-8111-111111111111')
  })
})

describe('a phone that is signed out while a reason stands on the order screen', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  async function aPhoneSignedOutWhileTheOrderScreenNamedAReason() {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-the-laptop-forgot')
    saveDraft({
      tableName: 'Tisch 12',
      note: null,
      lines: [{ catalogItemId: 'item-1', note: null, stationId: null, name: 'Bratwurst' }],
      clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
      deliveryModes: {},
    })
    saveSendProgress({
      state: 'failed',
      attempts: 1,
      settleOnSend: false,
      anAttemptWentUnanswered: true,
      failure: { key: 'review.sendFailed' },
    })
    const order = useOrderStore()
    const session = useSessionStore()
    session.watchForBeingSignedOut()
    vi.stubGlobal('fetch', vi.fn(async () => new Response('{}', { status: 401 })))

    await request('/api/orders', { method: 'POST', body: {}, token: session.deviceToken })
    return order
  }

  it('keeps the order closed for changes, because the laptop may still hold it', async () => {
    const order = await aPhoneSignedOutWhileTheOrderScreenNamedAReason()

    expect(order.changesAreRefused).toBe(true)
  })

  it('keeps the order and its identity, because it is the same order once the phone is back', async () => {
    const order = await aPhoneSignedOutWhileTheOrderScreenNamedAReason()

    expect(order.draft.lines).toHaveLength(1)
    expect(order.draft.clientOrderId).toBe('c0ffee00-1111-4111-8111-111111111111')
  })
})
