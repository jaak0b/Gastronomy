import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

vi.mock('@microsoft/signalr', async () => (await import('./../support/hubConnection')).signalrModuleFake())

const { navigate } = await import('../../src/router')
const App = (await import('../../src/App.vue')).default
const { testPlugins } = await import('../support/plugins')
const { TOKEN_STORAGE_KEY } = await import('../../src/stores/session')
const { useOrderStore } = await import('../../src/stores/order')
const { useSessionStore } = await import('../../src/stores/session')
const { restoreDraft, saveDraft, saveSendProgress } = await import('../../src/core/draftCart')

const SESSION = {
  deviceKind: 'staffMember',
  staffMember: { id: 'staff-1', name: 'Anna' },
  station: null,
  language: 'de',
}

const CATALOG = {
  categories: [
    { categoryId: 'category-getraenke', name: 'Getränke', colourHex: '#90CAF9', sortOrder: 1 },
  ],
  items: [
    {
      id: 'item-wasser',
      name: 'Wasser',
      categoryId: 'category-getraenke',
      priceCents: 200,
      sortOrder: 1,
      isAvailable: true,
      stationIds: ['station-bar'],
      productionMinutes: null,
    },
  ],
  stations: [{ id: 'station-bar', name: 'Bar', sortOrder: 1 }],
}

let phone: ReturnType<typeof mount> | null = null

function aLaptopThatForgotThisPhoneAfterItStarted(): void {
  vi.stubGlobal(
    'fetch',
    vi.fn(async (url: string) => {
      if (url === '/api/orders') {
        return new Response('{}', { status: 401 })
      }
      if (url === '/api/catalog') {
        return new Response(JSON.stringify(CATALOG), { status: 200 })
      }
      if (url === '/api/session') {
        return new Response(JSON.stringify(SESSION), { status: 200 })
      }
      if (url === '/api/enrolment/redeem') {
        return new Response(
          JSON.stringify({ ...SESSION, deviceToken: 'token-from-the-fresh-code' }),
          { status: 200 },
        )
      }
      return new Response(JSON.stringify({ stations: [], slices: [], tableNames: [] }), {
        status: 200,
      })
    }),
  )
}

async function aPhoneOnTheSummaryWithAnOrderOnIt() {
  localStorage.setItem(TOKEN_STORAGE_KEY, 'token-the-laptop-forgot')
  aLaptopThatForgotThisPhoneAfterItStarted()
  navigate('/review')
  phone = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
  await flushPromises()
  const order = useOrderStore()
  order.addItem({
    catalogItemId: 'item-wasser',
    note: null,
    stationId: 'station-bar',
    name: 'Wasser',
  })
  order.setTable('Tisch 5')
  return order
}

beforeEach(() => {
  setActivePinia(createPinia())
  localStorage.clear()
  document.body.innerHTML = ''
})

afterEach(() => {
  phone?.unmount()
  phone = null
  vi.unstubAllGlobals()
})

describe('a waiter who sends an order from a phone that was set up again while they were away', () => {
  it('is asked to set the phone up again instead of being told the laptop was not reached', async () => {
    const order = await aPhoneOnTheSummaryWithAnOrderOnIt()

    await order.send(false)
    await flushPromises()

    expect(phone?.find('.welcome').exists()).toBe(true)
  })

  it('still has the order on the phone, so it comes back once the new code is scanned', async () => {
    const order = await aPhoneOnTheSummaryWithAnOrderOnIt()

    await order.send(false)
    await flushPromises()

    expect(restoreDraft().draft.lines).toHaveLength(1)
  })

  it('sends that order under the identity it already had, so the laptop cannot store it twice', async () => {
    const order = await aPhoneOnTheSummaryWithAnOrderOnIt()
    const identity = order.draft.clientOrderId

    await order.send(false)
    await flushPromises()

    expect(restoreDraft().draft.clientOrderId).toBe(identity)
  })
})

describe('a phone whose waiter was set up again while the phone was switched off', () => {
  it('asks to be set up again as soon as the laptop refuses the check the app makes at the start', async () => {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-the-laptop-forgot')
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => new Response('{}', { status: 401 })),
    )
    navigate('/')

    phone = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()

    expect(phone.find('.welcome').exists()).toBe(true)
  })
})

describe('a phone that is set up again after the laptop refused the order it was sent', () => {
  async function aPhoneBackOnItsOrderAfterANewCodeWasScanned() {
    const order = await aPhoneOnTheSummaryWithAnOrderOnIt()
    await order.send(false)
    await flushPromises()

    await useSessionStore().redeem({ code: '123456' })
    await flushPromises()
    return order
  }

  it('puts the waiter back on the order they had started', async () => {
    await aPhoneBackOnItsOrderAfterANewCodeWasScanned()

    expect(phone?.find('.line-name').text()).toBe('1 x Wasser')
  })

  it('says nothing about a laptop that was not reached, because that is not what happened', async () => {
    await aPhoneBackOnItsOrderAfterANewCodeWasScanned()

    expect(phone?.find('.send-failure').exists()).toBe(false)
  })
})

describe('a waiter whose order was already frozen when the phone was set up again', () => {
  async function aFrozenOrderTheLaptopRefusesBecauseItForgotThePhone() {
    localStorage.setItem(TOKEN_STORAGE_KEY, 'token-the-laptop-forgot')
    saveDraft({
      tableName: 'Tisch 5',
      note: null,
      lines: [
        { catalogItemId: 'item-wasser', note: null, stationId: 'station-bar', name: 'Wasser' },
      ],
      clientOrderId: 'c0ffee00-1111-4111-8111-111111111111',
      deliveryModes: {},
    })
    saveSendProgress({
      state: 'sending',
      attempts: 1,
      settleOnSend: false,
      anAttemptWentUnanswered: false,
      failure: null,
    })
    aLaptopThatForgotThisPhoneAfterItStarted()
    navigate('/review')
    phone = mount(App, { global: { plugins: testPlugins() }, attachTo: document.body })
    await flushPromises()
    const order = useOrderStore()
    await order.sendAgain()
    await flushPromises()
    return order
  }

  it('is asked to set the phone up again, the same as any other waiter', async () => {
    await aFrozenOrderTheLaptopRefusesBecauseItForgotThePhone()

    expect(phone?.find('.welcome').exists()).toBe(true)
  })

  it('reads the same sentence as before on the summary once the new code is scanned', async () => {
    await aFrozenOrderTheLaptopRefusesBecauseItForgotThePhone()

    await useSessionStore().redeem({ code: '123456' })
    await flushPromises()

    expect(phone?.find('.send-failure .failure-message').text()).toBe(
      'Es ist nicht klar, ob die Bestellung angekommen ist. Tippen Sie auf "Erneut senden".',
    )
  })

  it('comes back to an order that still offers the one thing it offered before, sending again', async () => {
    await aFrozenOrderTheLaptopRefusesBecauseItForgotThePhone()

    await useSessionStore().redeem({ code: '123456' })
    await flushPromises()

    expect(phone?.find('.send-again').exists()).toBe(true)
  })
})
