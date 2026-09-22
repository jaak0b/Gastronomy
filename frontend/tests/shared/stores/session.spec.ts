import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createPinia, setActivePinia } from 'pinia'
import { TOKEN_STORAGE_KEY, useSessionStore } from '../../../src/shared/stores/session'
import {
  restoreDraft,
  saveDraft,
  saveSendProgress,
} from '../../../src/phone/core/draftCart'
import { useOrderStore } from '../../../src/phone/stores/order'
import { request } from '../../../src/shared/api/client'

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

  it('falls back to English for any other browser language', () => {
    withBrowserLanguage('fr-FR')

    expect(useSessionStore().language).toBe('en')
  })

  it('prefers the language stored on the device over the browser', () => {
    withBrowserLanguage('de-DE')
    localStorage.setItem('language', 'en')

    expect(useSessionStore().language).toBe('en')
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
            staffMember: { id: 'staff-1', name: 'Bernd' },
            language: 'de',
          }),
          { status: 200 },
        )
      }),
    )

    await useSessionStore().redeem({ code: 'abc123', name: 'Bernd' })

    expect(bodies).toEqual([
      {
        code: 'abc123',
        name: 'Bernd',
        userAgent: navigator.userAgent,
        previousDeviceToken: null,
      },
    ])
  })
})

describe('the token a browser gives up when it redeems a code', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  function aLaptopThatTakesTheCode(): unknown[] {
    const bodies: unknown[] = []
    vi.stubGlobal(
      'fetch',
      vi.fn(async (_url: string, options?: RequestInit) => {
        bodies.push(JSON.parse(String(options?.body ?? 'null')))
        return new Response(
          JSON.stringify({
            deviceId: 'device-of-the-phone',
            deviceToken: 'token-of-the-phone',
            staffMember: { id: 'staff-1', name: 'Anna' },
            station: null,
            language: 'de',
          }),
          { status: 200 },
        )
      }),
    )
    return bodies
  }

  it('is the one in the browser storage, so the laptop can retire it', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.of-the-tablet')
    const bodies = aLaptopThatTakesTheCode()

    await useSessionStore().redeem({ code: 'abc123' })

    expect(bodies).toEqual([
      {
        code: 'abc123',
        name: null,
        userAgent: navigator.userAgent,
        previousDeviceToken: 'lookup.of-the-tablet',
      },
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
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.the-laptop-forgot')
    saveDraft({
      festivalId: null,
      tableName: 'Tisch 12',
      lines: [{ catalogItemId: 'item-1', note: null, stationId: null, name: 'Bratwurst', stationName: '' }],
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

    expect(restoreDraft().draft.lines.length).toBeGreaterThan(0)
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
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup.the-laptop-forgot')
    saveDraft({
      festivalId: null,
      tableName: 'Tisch 12',
      lines: [{ catalogItemId: 'item-1', note: null, stationId: null, name: 'Bratwurst', stationName: '' }],
      clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
      deliveryModes: {},
    })
    saveSendProgress({
      state: 'failed',
      attempts: 1,
      failure: { key: 'review.sendFailed' },
      unresolvedAttempt: {
        clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
        tableName: 'Tisch 12',
        items: [
          {
            catalogItemId: 'item-1',
            unitPriceCents: 350,
            note: null,
            stationId: null,
            settlement: null,
          },
        ],
        deliveryModes: [],
      },
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

describe('a stored device token the laptop can never have issued', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    localStorage.clear()
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('is thrown away when it is JSON another build left behind, and the phone asks to be set up', () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, '{"deviceToken":"from-an-old-build"}')

    const session = useSessionStore()

    expect(session.deviceToken).toBeNull()
    expect(session.deviceSession).toEqual({ state: 'notSetUp' })
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('is thrown away when it is empty', () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, '')

    const session = useSessionStore()

    expect(session.deviceToken).toBeNull()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('is thrown away when it does not carry a lookup id and a secret', () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-from-an-old-build')

    const session = useSessionStore()

    expect(session.deviceToken).toBeNull()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('is thrown away when it carries no lookup id', () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, '.secret')

    const session = useSessionStore()

    expect(session.deviceToken).toBeNull()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('is thrown away when it carries no secret', () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup-id.')

    const session = useSessionStore()

    expect(session.deviceToken).toBeNull()
    expect(localStorage.getItem(TOKEN_STORAGE_KEY)).toBeNull()
  })

  it('is kept when it has the shape the laptop issues', () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'lookup-id.secret')

    const session = useSessionStore()

    expect(session.deviceToken).toBe('lookup-id.secret')
  })
})
